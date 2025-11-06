using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            // Clean up old directories from previous updates
            CleanupOldDirectories();
        }

        private void CleanupOldDirectories()
        {
            try
            {
                if (!Directory.Exists(_installPath))
                    return;

                // Find all directories ending with _old_* pattern
                var oldDirectories = Directory.GetDirectories(_installPath, "*_old_*");

                foreach (var oldDir in oldDirectories)
                {
                    try
                    {
                        Directory.Delete(oldDir, true);
                    }
                    catch
                    {
                        // Ignore - the directory might still be in use
                        // Will try again on next startup
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors - this is a best-effort operation
            }
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
                        process.Kill(true); // Kill entire process tree
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

        public async Task<bool> DownloadAndInstallAsync(GitHubAsset asset, LlamaVariant variant)
        {
            try
            {
                // Kill all llama-server processes before attempting file operations
                await KillAllLlamaServerProcessesAsync();

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

                // Extract to a temporary directory first for safety
                var tempExtractPath = Path.Combine(_installPath, $"{variant.Type}_temp_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempExtractPath);

                try
                {
                    StatusChanged?.Invoke(this, "Extracting files...");
                    ZipFile.ExtractToDirectory(zipPath, tempExtractPath);

                    // Now try to replace the old installation
                    if (Directory.Exists(extractPath))
                    {
                        StatusChanged?.Invoke(this, "Moving old installation...");

                        // Rename the old directory instead of deleting it
                        // This allows the installation to succeed even if files are locked
                        var oldPath = Path.Combine(_installPath, $"{variant.Type}_old_{DateTime.Now:yyyyMMdd_HHmmss}");
                        var renamed = false;

                        // Try to rename the directory with exponential backoff
                        // CUDA files especially may need more time for handles to be released
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
                            catch (UnauthorizedAccessException) when (i < maxAttempts - 1)
                            {
                                var delay = (int)Math.Pow(2, i) * 1000; // Exponential backoff: 1s, 2s, 4s, 8s
                                StatusChanged?.Invoke(this, $"Retrying to move old files (attempt {i + 2}/{maxAttempts})...");
                                await Task.Delay(delay);
                            }
                            catch (IOException) when (i < maxAttempts - 1)
                            {
                                var delay = (int)Math.Pow(2, i) * 1000; // Exponential backoff: 1s, 2s, 4s, 8s
                                StatusChanged?.Invoke(this, $"Retrying to move old files (attempt {i + 2}/{maxAttempts})...");
                                await Task.Delay(delay);
                            }
                        }

                        if (!renamed)
                        {
                            // Clean up temp directory
                            Directory.Delete(tempExtractPath, true);
                            throw new Exception($"Cannot move old installation at:\n{extractPath}\n\nThe files appear to be locked. Possible solutions:\n1. Restart the application and try again\n2. Manually delete the folder: {extractPath}\n3. Restart your computer if the issue persists\n\nNote: CUDA files may remain locked by Windows even after processes terminate.");
                        }

                        // Try to delete the old directory in the background, but don't fail if it's locked
                        // It will be cleaned up on next app startup
                        Task.Run(async () =>
                        {
                            await Task.Delay(2000); // Wait a bit for file handles to be released
                            try
                            {
                                Directory.Delete(oldPath, true);
                            }
                            catch
                            {
                                // Ignore - will be cleaned up on next startup
                            }
                        });
                    }

                    // Move the temp directory to the final location
                    StatusChanged?.Invoke(this, "Finalizing installation...");
                    Directory.Move(tempExtractPath, extractPath);
                }
                catch
                {
                    // Clean up temp directory if something went wrong
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

        public async Task<string> EnsureWebUIFilesAsync()
        {
            var webUIPath = Path.Combine(_installPath, "webui");
            var indexHtmlPath = Path.Combine(webUIPath, "index.html");

            // Check if WebUI files already exist
            if (File.Exists(indexHtmlPath))
            {
                StatusChanged?.Invoke(this, "WebUI files already present.");
                return webUIPath;
            }

            try
            {
                Directory.CreateDirectory(webUIPath);

                StatusChanged?.Invoke(this, "Downloading WebUI files from llama.cpp repository...");

                // Download the pre-built index.html.gz from llama.cpp repository
                const string webUIRawUrl = "https://raw.githubusercontent.com/ggml-org/llama.cpp/master/tools/server/public/index.html.gz";
                var gzipContent = await _httpClient.GetByteArrayAsync(webUIRawUrl);

                // Decompress the gzip file
                using (var compressedStream = new MemoryStream(gzipContent))
                using (var gzipStream = new System.IO.Compression.GZipStream(compressedStream, System.IO.Compression.CompressionMode.Decompress))
                using (var decompressedStream = new MemoryStream())
                {
                    await gzipStream.CopyToAsync(decompressedStream);
                    var htmlContent = System.Text.Encoding.UTF8.GetString(decompressedStream.ToArray());

                    // Save the decompressed index.html file
                    await File.WriteAllTextAsync(indexHtmlPath, htmlContent);
                }

                // Also download loading.html for fallback
                try
                {
                    const string loadingHtmlUrl = "https://raw.githubusercontent.com/ggml-org/llama.cpp/master/tools/server/public/loading.html";
                    var loadingContent = await _httpClient.GetStringAsync(loadingHtmlUrl);
                    await File.WriteAllTextAsync(Path.Combine(webUIPath, "loading.html"), loadingContent);
                }
                catch
                {
                    // Loading.html is optional, ignore if it fails
                }

                StatusChanged?.Invoke(this, "WebUI files downloaded successfully.");
                return webUIPath;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Warning: Could not download WebUI files: {ex.Message}");
                StatusChanged?.Invoke(this, "The Chat tab may not work without WebUI files.");

                // Return empty string to indicate failure
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
