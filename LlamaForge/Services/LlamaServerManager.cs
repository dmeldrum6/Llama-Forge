using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using LlamaForge.Models;

namespace LlamaForge.Services
{
    public class LlamaServerManager
    {
        private Process? _serverProcess;
        private readonly ServerConfig _config;
        private readonly string _executablePath;

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
                    }
                };

                _serverProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        OutputReceived?.Invoke(this, e.Data);
                    }
                };

                _serverProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        ErrorReceived?.Invoke(this, e.Data);
                    }
                };

                _serverProcess.Exited += (sender, e) =>
                {
                    ServerStatusChanged?.Invoke(this, false);
                };

                _serverProcess.EnableRaisingEvents = true;
                _serverProcess.Start();
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();

                ServerStatusChanged?.Invoke(this, true);
                OutputReceived?.Invoke(this, $"Server started with PID: {_serverProcess.Id}");
                OutputReceived?.Invoke(this, $"Command: {_executablePath} {arguments}");

                // Wait a bit to see if it starts successfully
                await Task.Delay(2000);

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

                _serverProcess.Kill(true); // Kill entire process tree
                _serverProcess.WaitForExit(5000);

                if (!_serverProcess.HasExited)
                {
                    _serverProcess.Kill(); // Force kill if still running
                }

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
            // Basic Configuration
            var args = $"-m \"{_config.ModelPath}\" ";
            args += $"--host {_config.Host} ";
            args += $"--port {_config.Port} ";
            args += $"-c {_config.ContextSize} ";

            // Performance Settings
            args += $"-t {_config.Threads} ";
            args += $"-b {_config.BatchSize} ";
            args += $"-tb {_config.BatchThreads} ";
            args += $"-np {_config.ParallelSlots} ";

            if (_config.ContinuousBatching)
            {
                args += "-cb ";
            }

            // Always pass -ngl to ensure GPU layers setting is respected
            // -ngl 0 = CPU only, -ngl N = offload N layers to GPU
            args += $"-ngl {_config.GpuLayers} ";

            // Memory Management
            if (_config.MemoryLock)
            {
                args += "--mlock ";
            }

            if (_config.DisableMemoryMapping)
            {
                args += "--no-mmap ";
            }

            // Server Features
            if (!string.IsNullOrWhiteSpace(_config.ModelAlias))
            {
                args += $"-a \"{_config.ModelAlias}\" ";
            }

            if (!string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                args += $"--api-key \"{_config.ApiKey}\" ";
            }

            args += $"-to {_config.Timeout} ";

            if (_config.EnableEmbeddings)
            {
                args += "--embedding ";
            }

            // Enable verbose logging to diagnose chat issues
            args += "--verbose ";

            // Enable Jinja template for chat formatting (required for WebUI chat)
            args += "--jinja ";

            // WebUI Path (if provided, serve static files from this directory)
            if (!string.IsNullOrWhiteSpace(_config.WebUIPath) && Directory.Exists(_config.WebUIPath))
            {
                args += $"--path \"{_config.WebUIPath}\" ";
            }

            // Additional custom arguments
            if (!string.IsNullOrWhiteSpace(_config.AdditionalArgs))
            {
                args += _config.AdditionalArgs;
            }

            return args.Trim();
        }

        public void Dispose()
        {
            StopServer();
        }
    }
}
