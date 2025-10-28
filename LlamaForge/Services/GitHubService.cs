using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LlamaForge.Models;
using Newtonsoft.Json;

namespace LlamaForge.Services
{
    public class GitHubService
    {
        private const string LlamaCppRepo = "ggerganov/llama.cpp";
        private const string GitHubApiUrl = "https://api.github.com";
        private readonly HttpClient _httpClient;
        private readonly string _installPath;

        public event EventHandler<DownloadProgressEventArgs>? DownloadProgressChanged;
        public event EventHandler<string>? StatusChanged;

        public GitHubService(string installPath)
        {
            _installPath = installPath;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "LlamaForge");

            Directory.CreateDirectory(_installPath);
        }

        public async Task<GitHubRelease?> GetLatestReleaseAsync()
        {
            try
            {
                StatusChanged?.Invoke(this, "Checking for latest release...");
                var url = $"{GitHubApiUrl}/repos/{LlamaCppRepo}/releases/latest";
                var response = await _httpClient.GetStringAsync(url);
                var release = JsonConvert.DeserializeObject<dynamic>(response);

                if (release == null) return null;

                var result = new GitHubRelease
                {
                    TagName = release.tag_name,
                    Name = release.name,
                    PublishedAt = release.published_at,
                    Body = release.body ?? string.Empty,
                    Assets = new List<GitHubAsset>()
                };

                foreach (var asset in release.assets)
                {
                    result.Assets.Add(new GitHubAsset
                    {
                        Name = asset.name,
                        BrowserDownloadUrl = asset.browser_download_url,
                        Size = asset.size
                    });
                }

                StatusChanged?.Invoke(this, $"Found release: {result.TagName}");
                return result;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Error checking for updates: {ex.Message}");
                return null;
            }
        }

        public async Task<List<GitHubAsset>> GetAvailableAssetsForVariantAsync(LlamaVariant variant)
        {
            var release = await GetLatestReleaseAsync();
            if (release == null) return new List<GitHubAsset>();

            var pattern = variant.AssetNamePattern.Replace("*", ".*");
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);

            return release.Assets
                .Where(a => regex.IsMatch(a.Name))
                .ToList();
        }

        public async Task<bool> DownloadAndInstallAsync(GitHubAsset asset, LlamaVariant variant)
        {
            try
            {
                StatusChanged?.Invoke(this, $"Downloading {asset.Name}...");

                var zipPath = Path.Combine(_installPath, asset.Name);
                var extractPath = Path.Combine(_installPath, variant.Type.ToString());

                // Download the file
                using (var response = await _httpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var downloadedBytes = 0L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            downloadedBytes += bytesRead;

                            var progress = totalBytes > 0 ? (int)((downloadedBytes * 100) / totalBytes) : 0;
                            DownloadProgressChanged?.Invoke(this, new DownloadProgressEventArgs(progress, downloadedBytes, totalBytes));
                        }
                    }
                }

                StatusChanged?.Invoke(this, $"Extracting {asset.Name}...");

                // Extract the zip file
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }
                Directory.CreateDirectory(extractPath);

                ZipFile.ExtractToDirectory(zipPath, extractPath);

                // Clean up zip file
                File.Delete(zipPath);

                // Save version info
                var versionFile = Path.Combine(extractPath, "version.txt");
                await File.WriteAllTextAsync(versionFile, $"{asset.Name}\n{DateTime.Now}");

                StatusChanged?.Invoke(this, "Installation completed successfully!");
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Error during download/installation: {ex.Message}");
                return false;
            }
        }

        public string? GetInstalledVersion(LlamaVariant variant)
        {
            var extractPath = Path.Combine(_installPath, variant.Type.ToString());
            var versionFile = Path.Combine(extractPath, "version.txt");

            if (File.Exists(versionFile))
            {
                var lines = File.ReadAllLines(versionFile);
                return lines.Length > 0 ? lines[0] : null;
            }

            return null;
        }

        public string GetServerExecutablePath(LlamaVariant variant)
        {
            var extractPath = Path.Combine(_installPath, variant.Type.ToString());

            // Check if directory exists first
            if (!Directory.Exists(extractPath))
            {
                return Path.Combine(extractPath, "bin", "llama-server.exe");
            }

            // Look for llama-server.exe in the extracted directory and subdirectories
            var serverExe = Directory.GetFiles(extractPath, "llama-server.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (serverExe != null)
                return serverExe;

            // Fallback to old naming convention
            serverExe = Directory.GetFiles(extractPath, "server.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            return serverExe ?? Path.Combine(extractPath, "bin", "llama-server.exe");
        }
    }

    public class DownloadProgressEventArgs : EventArgs
    {
        public int ProgressPercentage { get; }
        public long BytesDownloaded { get; }
        public long TotalBytes { get; }

        public DownloadProgressEventArgs(int progressPercentage, long bytesDownloaded, long totalBytes)
        {
            ProgressPercentage = progressPercentage;
            BytesDownloaded = bytesDownloaded;
            TotalBytes = totalBytes;
        }
    }
}
