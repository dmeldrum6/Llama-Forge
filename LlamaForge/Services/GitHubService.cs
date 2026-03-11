using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
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

            // Clean up old directories from previous updates
            CleanupOldDirectories();
        }

        private void CleanupOldDirectories()
        {
            try
            {
                if (!Directory.Exists(_installPath))
                    return;

                var oldDirectories = Directory.GetDirectories(_installPath, "*_old_*");

                foreach (var oldDir in oldDirectories)
                {
                    try
                    {
                        Directory.Delete(oldDir, true);
                    }
                    catch
                    {
                        // Ignore - the directory might still be in use; retry on next startup
                    }
                }
            }
            catch
            {
                // Best-effort — ignore cleanup errors
            }
        }

        public async Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                StatusChanged?.Invoke(this, "Checking for latest release...");
                var url = $"{GitHubApiUrl}/repos/{LlamaCppRepo}/releases/latest";
                var json = await _httpClient.GetStringAsync(url, cancellationToken);
                var release = JsonConvert.DeserializeObject<GitHubRelease>(json);

                if (release != null)
                    StatusChanged?.Invoke(this, $"Found release: {release.TagName}");

                return release;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Error checking for updates: {ex.Message}");
                return null;
            }
        }

        public async Task<List<GitHubAsset>> GetAvailableAssetsForVariantAsync(
            LlamaVariant variant,
            CancellationToken cancellationToken = default)
        {
            var release = await GetLatestReleaseAsync(cancellationToken);
            if (release == null) return new List<GitHubAsset>();

            var pattern = variant.AssetNamePattern.Replace("*", ".*");
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);

            return release.Assets
                .Where(a => regex.IsMatch(a.Name))
                .ToList();
        }

        private async Task KillAllLlamaServerProcessesAsync()
        {
            try
            {
                StatusChanged?.Invoke(this, "Checking for running llama-server processes...");

                var processes = Process.GetProcessesByName("llama-server");

                if (processes.Length == 0)
                {
                    StatusChanged?.Invoke(this, "No llama-server processes found.");
                    return;
                }

                StatusChanged?.Invoke(this, $"Found {processes.Length} llama-server process(es). Terminating...");

                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(3000);
                    }
                    catch (Exception ex)
                    {
                        StatusChanged?.Invoke(this, $"Warning: Could not terminate process {process.Id}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                // Wait for file handles to be released
                StatusChanged?.Invoke(this, "Waiting for file handles to be released...");
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Warning during process cleanup: {ex.Message}");
            }
        }

        public async Task<bool> DownloadAndInstallAsync(
            GitHubAsset asset,
            LlamaVariant variant,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Kill all llama-server processes before attempting file operations
                await KillAllLlamaServerProcessesAsync();

                StatusChanged?.Invoke(this, $"Downloading {asset.Name}...");

                var zipPath = Path.Combine(_installPath, asset.Name);
                var extractPath = Path.Combine(_installPath, variant.Type.ToString());

                // Download the file
                using (var response = await _httpClient.GetAsync(
                    asset.BrowserDownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var downloadedBytes = 0L;

                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    var buffer = new byte[8192];
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        downloadedBytes += bytesRead;

                        var progress = totalBytes > 0 ? (int)((downloadedBytes * 100) / totalBytes) : 0;
                        DownloadProgressChanged?.Invoke(this, new DownloadProgressEventArgs(progress, downloadedBytes, totalBytes));
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                StatusChanged?.Invoke(this, $"Extracting {asset.Name}...");

                // Extract to a temporary directory first for safety
                var tempExtractPath = Path.Combine(_installPath, $"{variant.Type}_temp_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempExtractPath);

                try
                {
                    ZipFile.ExtractToDirectory(zipPath, tempExtractPath);

                    // Try to replace the old installation
                    if (Directory.Exists(extractPath))
                    {
                        StatusChanged?.Invoke(this, "Moving old installation...");

                        var oldPath = Path.Combine(_installPath, $"{variant.Type}_old_{DateTime.Now:yyyyMMdd_HHmmss}");
                        var renamed = false;

                        const int maxAttempts = 5;
                        for (int i = 0; i < maxAttempts; i++)
                        {
                            try
                            {
                                Directory.Move(extractPath, oldPath);
                                renamed = true;
                                StatusChanged?.Invoke(this, "Old installation moved successfully.");
                                break;
                            }
                            catch (Exception) when (i < maxAttempts - 1)
                            {
                                var delay = (int)Math.Pow(2, i) * 1000;
                                StatusChanged?.Invoke(this, $"Retrying to move old files (attempt {i + 2}/{maxAttempts})...");
                                await Task.Delay(delay, cancellationToken);
                            }
                        }

                        if (!renamed)
                        {
                            Directory.Delete(tempExtractPath, true);
                            throw new IOException(
                                $"Cannot move old installation at:\n{extractPath}\n\n" +
                                "The files appear to be locked. Possible solutions:\n" +
                                "1. Restart the application and try again\n" +
                                $"2. Manually delete the folder: {extractPath}\n" +
                                "3. Restart your computer if the issue persists");
                        }

                        // Try to delete the old directory in the background
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(2000);
                            try { Directory.Delete(oldPath, true); } catch { }
                        });
                    }

                    StatusChanged?.Invoke(this, "Finalizing installation...");
                    Directory.Move(tempExtractPath, extractPath);
                }
                catch
                {
                    if (Directory.Exists(tempExtractPath))
                    {
                        try { Directory.Delete(tempExtractPath, true); } catch { }
                    }
                    throw;
                }

                // Clean up zip file
                File.Delete(zipPath);

                // Save version info
                var versionFile = Path.Combine(extractPath, "version.txt");
                await File.WriteAllTextAsync(versionFile, $"{asset.Name}\n{DateTime.Now}");

                StatusChanged?.Invoke(this, "Installation completed successfully!");
                return true;
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke(this, "Download cancelled.");
                return false;
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

            if (!Directory.Exists(extractPath))
                return Path.Combine(extractPath, "bin", "llama-server.exe");

            var serverExe = Directory.GetFiles(extractPath, "llama-server.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (serverExe != null)
                return serverExe;

            // Fallback to old naming convention
            serverExe = Directory.GetFiles(extractPath, "server.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            return serverExe ?? Path.Combine(extractPath, "bin", "llama-server.exe");
        }

        public async Task<string> EnsureWebUIFilesAsync(CancellationToken cancellationToken = default)
        {
            var webUIPath = Path.Combine(_installPath, "webui");
            var indexHtmlPath = Path.Combine(webUIPath, "index.html");

            if (File.Exists(indexHtmlPath))
            {
                StatusChanged?.Invoke(this, "WebUI files already present.");
                return webUIPath;
            }

            try
            {
                Directory.CreateDirectory(webUIPath);

                StatusChanged?.Invoke(this, "Downloading WebUI files from llama.cpp repository...");

                const string webUIRawUrl = "https://raw.githubusercontent.com/ggml-org/llama.cpp/master/tools/server/public/index.html.gz";
                var gzipContent = await _httpClient.GetByteArrayAsync(webUIRawUrl);

                using var compressedStream = new MemoryStream(gzipContent);
                using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                using var decompressedStream = new MemoryStream();

                await gzipStream.CopyToAsync(decompressedStream, cancellationToken);
                var htmlContent = System.Text.Encoding.UTF8.GetString(decompressedStream.ToArray());
                await File.WriteAllTextAsync(indexHtmlPath, htmlContent, cancellationToken);

                try
                {
                    const string loadingHtmlUrl = "https://raw.githubusercontent.com/ggml-org/llama.cpp/master/tools/server/public/loading.html";
                    var loadingContent = await _httpClient.GetStringAsync(loadingHtmlUrl);
                    await File.WriteAllTextAsync(Path.Combine(webUIPath, "loading.html"), loadingContent, cancellationToken);
                }
                catch
                {
                    // loading.html is optional
                }

                StatusChanged?.Invoke(this, "WebUI files downloaded successfully.");
                return webUIPath;
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke(this, "WebUI download cancelled.");
                return string.Empty;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Warning: Could not download WebUI files: {ex.Message}");
                StatusChanged?.Invoke(this, "The Chat tab may not work without WebUI files.");
                return string.Empty;
            }
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
