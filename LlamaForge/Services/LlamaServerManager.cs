using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LlamaForge.Models;

namespace LlamaForge.Services
{
    public class LlamaServerManager : IDisposable
    {
        private Process? _serverProcess;
        private readonly ServerConfig _config;
        private readonly string _executablePath;
        private bool _disposed;

        public event EventHandler<string>? OutputReceived;
        public event EventHandler<string>? ErrorReceived;
        public event EventHandler<bool>? ServerStatusChanged;

        public bool IsRunning => _serverProcess != null && !_serverProcess.HasExited;

        public LlamaServerManager(ServerConfig config, string executablePath)
        {
            _config = config;
            _executablePath = executablePath;
        }

        public async Task<bool> StartServerAsync()
        {
            if (IsRunning)
            {
                OutputReceived?.Invoke(this, "Server is already running.");
                return false;
            }

            if (!File.Exists(_executablePath))
            {
                ErrorReceived?.Invoke(this, $"Server executable not found at: {_executablePath}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_config.ModelPath) || !File.Exists(_config.ModelPath))
            {
                ErrorReceived?.Invoke(this, $"Model file not found at: {_config.ModelPath}");
                return false;
            }

            try
            {
                var arguments = BuildCommandLineArguments();
                var safeArguments = BuildSafeCommandLineArguments();

                _serverProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _executablePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(_executablePath)
                    },
                    EnableRaisingEvents = true
                };

                _serverProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        OutputReceived?.Invoke(this, e.Data);
                };

                _serverProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        ErrorReceived?.Invoke(this, e.Data);
                };

                _serverProcess.Exited += (sender, e) =>
                {
                    ServerStatusChanged?.Invoke(this, false);
                };

                _serverProcess.Start();
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();

                ServerStatusChanged?.Invoke(this, true);
                OutputReceived?.Invoke(this, $"Server started with PID: {_serverProcess.Id}");
                // Log sanitized command (no API key value)
                OutputReceived?.Invoke(this, $"Command: {_executablePath} {safeArguments}");

                // Brief pause to detect immediate crashes (e.g. missing model file, port already in use).
                // The caller is responsible for polling health to confirm the server is actually ready.
                await Task.Delay(500);

                if (_serverProcess.HasExited)
                {
                    ErrorReceived?.Invoke(this, $"Server process exited immediately with code: {_serverProcess.ExitCode}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorReceived?.Invoke(this, $"Failed to start server: {ex.Message}");
                return false;
            }
        }

        public void StopServer()
        {
            if (_serverProcess == null || _serverProcess.HasExited)
            {
                OutputReceived?.Invoke(this, "Server is not running.");
                return;
            }

            try
            {
                OutputReceived?.Invoke(this, "Stopping server...");

                _serverProcess.Kill(entireProcessTree: true);
                _serverProcess.WaitForExit(5000);

                if (!_serverProcess.HasExited)
                    _serverProcess.Kill();

                _serverProcess.Dispose();
                _serverProcess = null;

                ServerStatusChanged?.Invoke(this, false);
                OutputReceived?.Invoke(this, "Server stopped.");
            }
            catch (Exception ex)
            {
                ErrorReceived?.Invoke(this, $"Error stopping server: {ex.Message}");
            }
        }

        private string BuildCommandLineArguments()
        {
            var args = new StringBuilder();
            AppendCoreArguments(args, redactApiKey: false);
            return args.ToString().Trim();
        }

        /// <summary>Builds a version of the command line with the API key value replaced by
        /// a placeholder, safe for logging.</summary>
        private string BuildSafeCommandLineArguments()
        {
            var args = new StringBuilder();
            AppendCoreArguments(args, redactApiKey: true);
            return args.ToString().Trim();
        }

        private void AppendCoreArguments(StringBuilder args, bool redactApiKey)
        {
            // Basic Configuration
            args.Append($"-m \"{_config.ModelPath}\" ");
            args.Append($"--host {_config.Host} ");
            args.Append($"--port {_config.Port} ");
            args.Append($"-c {_config.ContextSize} ");

            // Performance Settings
            args.Append($"-t {_config.Threads} ");
            args.Append($"-b {_config.BatchSize} ");
            args.Append($"-tb {_config.BatchThreads} ");
            args.Append($"-np {_config.ParallelSlots} ");

            if (_config.ContinuousBatching)
                args.Append("-cb ");

            // Always pass -ngl to ensure GPU layers setting is respected
            args.Append($"-ngl {_config.GpuLayers} ");

            // Memory Management
            if (_config.MemoryLock)
                args.Append("--mlock ");

            if (_config.DisableMemoryMapping)
                args.Append("--no-mmap ");

            // Server Features
            if (!string.IsNullOrWhiteSpace(_config.ModelAlias))
                args.Append($"-a \"{_config.ModelAlias}\" ");

            if (!string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                if (redactApiKey)
                    args.Append("--api-key <redacted> ");
                else
                    args.Append($"--api-key \"{_config.ApiKey}\" ");
            }

            args.Append($"-to {_config.Timeout} ");

            if (_config.EnableEmbeddings)
                args.Append("--embedding ");

            if (_config.VerboseLogging)
                args.Append("--verbose ");

            // Enable Jinja template for chat formatting (required for WebUI chat)
            args.Append("--jinja ");

            // WebUI Path (if provided, serve static files from this directory)
            if (!string.IsNullOrWhiteSpace(_config.WebUIPath) && Directory.Exists(_config.WebUIPath))
                args.Append($"--path \"{_config.WebUIPath}\" ");

            // Additional custom arguments
            if (!string.IsNullOrWhiteSpace(_config.AdditionalArgs))
                args.Append(_config.AdditionalArgs);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopServer();
        }
    }
}
