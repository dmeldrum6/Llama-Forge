using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LlamaForge.Models;
using LlamaForge.Services;
using Microsoft.Win32;

namespace LlamaForge.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly GitHubService _githubService;
        private readonly SettingsService _settingsService;
        private LlamaServerManager? _serverManager;
        private LlamaChatClient? _chatClient;

        // Observable Collections
        public ObservableCollection<ChatMessage> ChatMessages { get; } = new();
        public ObservableCollection<string> ServerLogs { get; } = new();
        public ObservableCollection<LlamaVariant> AvailableVariants { get; } = new();
        public ObservableCollection<DownloadableVariant> DownloadableVariants { get; } = new();

        // Properties
        private string _userInput = string.Empty;
        public string UserInput
        {
            get => _userInput;
            set { _userInput = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private bool _isServerRunning;
        public bool IsServerRunning
        {
            get => _isServerRunning;
            set { _isServerRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartServer)); OnPropertyChanged(nameof(CanStopServer)); }
        }

        private string _webUILocalPath = string.Empty;
        public string WebUILocalPath
        {
            get => _webUILocalPath;
            set { _webUILocalPath = value; OnPropertyChanged(); }
        }

        private bool _isServerReady;
        public bool IsServerReady
        {
            get => _isServerReady;
            set { _isServerReady = value; OnPropertyChanged(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSendMessage)); }
        }

        private int _downloadProgress;
        public int DownloadProgress
        {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set { _isDownloading = value; OnPropertyChanged(); }
        }

        private LlamaVariant? _selectedVariant;
        public LlamaVariant? SelectedVariant
        {
            get => _selectedVariant;
            set
            {
                _selectedVariant = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InstalledVersion));
                UpdateServerExecutablePath();
                SaveSettings();
            }
        }

        private string _modelPath = string.Empty;
        public string ModelPath
        {
            get => _modelPath;
            set { _modelPath = value; OnPropertyChanged(); Config.ModelPath = value; SaveSettings(); }
        }

        private string _host = "127.0.0.1";
        public string Host
        {
            get => _host;
            set { _host = value; OnPropertyChanged(); Config.Host = value; SaveSettings(); }
        }

        private int _port = 8080;
        public int Port
        {
            get => _port;
            set { _port = value; OnPropertyChanged(); Config.Port = value; SaveSettings(); }
        }

        private int _contextSize = 2048;
        public int ContextSize
        {
            get => _contextSize;
            set { _contextSize = value; OnPropertyChanged(); Config.ContextSize = value; SaveSettings(); }
        }

        private int _threads = 4;
        public int Threads
        {
            get => _threads;
            set { _threads = value; OnPropertyChanged(); Config.Threads = value; SaveSettings(); }
        }

        private int _gpuLayers = 0;
        public int GpuLayers
        {
            get => _gpuLayers;
            set { _gpuLayers = value; OnPropertyChanged(); Config.GpuLayers = value; SaveSettings(); }
        }

        private int _batchSize = 512;
        public int BatchSize
        {
            get => _batchSize;
            set { _batchSize = value; OnPropertyChanged(); Config.BatchSize = value; SaveSettings(); }
        }

        private int _batchThreads = 4;
        public int BatchThreads
        {
            get => _batchThreads;
            set { _batchThreads = value; OnPropertyChanged(); Config.BatchThreads = value; SaveSettings(); }
        }

        private int _parallelSlots = 1;
        public int ParallelSlots
        {
            get => _parallelSlots;
            set { _parallelSlots = value; OnPropertyChanged(); Config.ParallelSlots = value; SaveSettings(); }
        }

        private bool _continuousBatching = false;
        public bool ContinuousBatching
        {
            get => _continuousBatching;
            set { _continuousBatching = value; OnPropertyChanged(); Config.ContinuousBatching = value; SaveSettings(); }
        }

        private bool _memoryLock = false;
        public bool MemoryLock
        {
            get => _memoryLock;
            set { _memoryLock = value; OnPropertyChanged(); Config.MemoryLock = value; SaveSettings(); }
        }

        private bool _disableMemoryMapping = false;
        public bool DisableMemoryMapping
        {
            get => _disableMemoryMapping;
            set { _disableMemoryMapping = value; OnPropertyChanged(); Config.DisableMemoryMapping = value; SaveSettings(); }
        }

        private string _modelAlias = string.Empty;
        public string ModelAlias
        {
            get => _modelAlias;
            set { _modelAlias = value; OnPropertyChanged(); Config.ModelAlias = value; SaveSettings(); }
        }

        private string _apiKey = string.Empty;
        public string ApiKey
        {
            get => _apiKey;
            set { _apiKey = value; OnPropertyChanged(); Config.ApiKey = value; SaveSettings(); }
        }

        private int _timeout = 600;
        public int Timeout
        {
            get => _timeout;
            set { _timeout = value; OnPropertyChanged(); Config.Timeout = value; SaveSettings(); }
        }

        private bool _enableEmbeddings = false;
        public bool EnableEmbeddings
        {
            get => _enableEmbeddings;
            set { _enableEmbeddings = value; OnPropertyChanged(); Config.EnableEmbeddings = value; SaveSettings(); }
        }

        private string _additionalArgs = string.Empty;
        public string AdditionalArgs
        {
            get => _additionalArgs;
            set { _additionalArgs = value; OnPropertyChanged(); Config.AdditionalArgs = value; SaveSettings(); }
        }

        private string _systemPrompt = "You are a helpful assistant";
        public string SystemPrompt
        {
            get => _systemPrompt;
            set { _systemPrompt = value; OnPropertyChanged(); Config.SystemPrompt = value; SaveSettings(); }
        }

        private ModelInfo? _currentModelInfo;
        public ModelInfo? CurrentModelInfo
        {
            get => _currentModelInfo;
            set { _currentModelInfo = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasModelInfo)); }
        }

        public bool HasModelInfo => CurrentModelInfo != null;

        private bool _isDarkTheme = true;
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set { _isDarkTheme = value; OnPropertyChanged(); }
        }

        public ServerConfig Config { get; } = new();

        public string InstalledVersion => SelectedVariant != null ? _githubService.GetInstalledVersion(SelectedVariant) ?? "Not installed" : "Select a variant";

        public bool CanStartServer => !IsServerRunning && !string.IsNullOrWhiteSpace(ModelPath);
        public bool CanStopServer => IsServerRunning;
        public bool CanSendMessage => IsServerRunning && !IsBusy && !string.IsNullOrWhiteSpace(UserInput);

        // Commands
        public ICommand StartServerCommand { get; }
        public ICommand StopServerCommand { get; }
        public ICommand SendMessageCommand { get; }
        public ICommand BrowseModelCommand { get; }
        public ICommand CheckUpdatesCommand { get; }
        public ICommand DownloadVariantCommand { get; }
        public ICommand ClearChatCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand AutoDetectThreadsCommand { get; }

        public MainViewModel()
        {
            try
            {
                Console.WriteLine("=== MainViewModel constructor started ===");
                System.Diagnostics.Debug.WriteLine("=== MainViewModel constructor started ===");

                var installPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LlamaForge",
                    "llama-cpp"
                );

                Console.WriteLine($"Install path: {installPath}");
                System.Diagnostics.Debug.WriteLine($"Install path: {installPath}");

                _githubService = new GitHubService(installPath);
                _githubService.DownloadProgressChanged += (s, e) => DownloadProgress = e.ProgressPercentage;
                _githubService.StatusChanged += (s, message) => AddServerLog(message);

                _settingsService = new SettingsService();

                Console.WriteLine("GitHubService initialized");
                System.Diagnostics.Debug.WriteLine("GitHubService initialized");

                // Initialize available variants
                foreach (var variant in LlamaVariant.GetAvailableVariants())
                {
                    AvailableVariants.Add(variant);
                    Console.WriteLine($"Added variant: {variant.DisplayName}");
                }

                Console.WriteLine($"Total variants: {AvailableVariants.Count}");
                System.Diagnostics.Debug.WriteLine($"Total variants: {AvailableVariants.Count}");

                // Initialize downloadable variants
                foreach (var variant in AvailableVariants)
                {
                    var downloadableVariant = new DownloadableVariant
                    {
                        Variant = variant,
                        IsSelected = false,
                        InstalledVersion = _githubService.GetInstalledVersion(variant) ?? "Not installed",
                        LatestVersion = "Unknown",
                        DownloadProgress = 0,
                        IsDownloading = false,
                        StatusMessage = "Click 'Check for Updates' to check latest version"
                    };
                    DownloadableVariants.Add(downloadableVariant);
                }

                // If you want to automatically check for updates in the background after UI loads
                /*
                Task.Run(async () =>
                {
                    await Task.Delay(1000); // Wait a bit for UI to fully load
                    await CheckForUpdatesAsync();
                });
                */

                // Commands
                StartServerCommand = new RelayCommand(async _ => await StartServerAsync(), _ => CanStartServer);
                StopServerCommand = new RelayCommand(_ => StopServer(), _ => CanStopServer);
                SendMessageCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => CanSendMessage);
                BrowseModelCommand = new RelayCommand(_ => BrowseModel());
                CheckUpdatesCommand = new RelayCommand(async _ => await CheckForUpdatesAsync());
                DownloadVariantCommand = new RelayCommand(async _ => await DownloadSelectedVariantAsync());
                ClearChatCommand = new RelayCommand(_ => ChatMessages.Clear());
                ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
                AutoDetectThreadsCommand = new RelayCommand(_ => AutoDetectThreads());

                Console.WriteLine("Commands initialized");
                System.Diagnostics.Debug.WriteLine("Commands initialized");

                // Load settings
                LoadSettings();

                Console.WriteLine("=== MainViewModel constructor completed ===");
                System.Diagnostics.Debug.WriteLine("=== MainViewModel constructor completed ===");

                // Test logging after constructor completes
                AddServerLog("Llama Forge initialized successfully");
            }
            catch (Exception ex)
            {
                var errorMsg = $"FATAL ERROR in MainViewModel constructor: {ex.GetType().Name} - {ex.Message}\nStack trace: {ex.StackTrace}";
                Console.WriteLine(errorMsg);
                System.Diagnostics.Debug.WriteLine(errorMsg);
                MessageBox.Show($"Critical error during initialization:\n\n{ex.Message}\n\nThe application may not function correctly.", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StartServerAsync()
        {
            try
            {
                AddServerLog("=== StartServerAsync called ===");

                if (SelectedVariant == null)
                {
                    StatusMessage = "Please select a variant first.";
                    AddServerLog("ERROR: No variant selected");
                    return;
                }

                AddServerLog($"Selected variant: {SelectedVariant.DisplayName}");

                var executablePath = _githubService.GetServerExecutablePath(SelectedVariant);
                AddServerLog($"Executable path: {executablePath}");

                if (!File.Exists(executablePath))
                {
                    StatusMessage = $"Server executable not found. Please download {SelectedVariant.DisplayName} first.";
                    AddServerLog($"ERROR: Executable not found at: {executablePath}");
                    return;
                }

                AddServerLog("Executable file exists, proceeding with server startup");

                IsBusy = true;
                StatusMessage = "Starting server...";

                AddServerLog($"Model path: {Config.ModelPath}");
                AddServerLog($"Server config - Host: {Config.Host}, Port: {Config.Port}, Context: {Config.ContextSize}, Threads: {Config.Threads}, GPU Layers: {Config.GpuLayers}");

                // Ensure WebUI files are downloaded
                AddServerLog("Checking for WebUI files...");
                var webUIPath = await _githubService.EnsureWebUIFilesAsync();
                if (!string.IsNullOrEmpty(webUIPath))
                {
                    Config.WebUIPath = webUIPath;
                    WebUILocalPath = Path.Combine(webUIPath, "index.html");
                    AddServerLog($"WebUI path set to: {webUIPath}");
                    AddServerLog($"WebUI file path: {WebUILocalPath}");
                }
                else
                {
                    AddServerLog("WARNING: WebUI files could not be downloaded. Chat tab may not work.");
                }

                _serverManager = new LlamaServerManager(Config, executablePath);
                _serverManager.OutputReceived += (s, log) => AddServerLog(log);
                _serverManager.ErrorReceived += (s, log) => AddServerLog(log);
                _serverManager.ServerStatusChanged += (s, running) =>
                {
                    AddServerLog($"Server status changed: {(running ? "Running" : "Stopped")}");
                    try
                    {
                        if (Application.Current?.Dispatcher != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                IsServerRunning = running;
                                StatusMessage = running ? "Server is running" : "Server stopped";
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        AddServerLog($"ERROR in ServerStatusChanged handler: {ex.Message}");
                    }
                };

                AddServerLog("Calling _serverManager.StartServerAsync()...");
                var success = await _serverManager.StartServerAsync();
                AddServerLog($"StartServerAsync returned: {success}");

                if (success)
                {
                    AddServerLog($"Creating LlamaChatClient for {Host}:{Port}");
                    _chatClient = new LlamaChatClient(Host, Port);
                    _chatClient.ResponseChunkReceived += OnResponseChunkReceived;
                    _chatClient.ErrorOccurred += (s, error) => AddServerLog($"Chat Error: {error}");

                    // Wait for server to be ready
                    AddServerLog("Waiting 3 seconds for server to initialize...");
                    await Task.Delay(3000);

                    AddServerLog("Performing health check...");
                    var isHealthy = await _chatClient.CheckHealthAsync();
                    AddServerLog($"Health check result: {isHealthy}");

                    if (isHealthy)
                    {
                        StatusMessage = "Server is ready!";
                        AddServerLog("=== Server startup completed successfully ===");

                        // Signal that server is ready for WebUI navigation
                        IsServerReady = true;
                        AddServerLog("Server ready - WebUI can now be loaded");

                        // Run diagnostics
                        AddServerLog("=== Running Chat Diagnostics ===");

                        try
                        {
                            AddServerLog("Checking model info...");
                            var modelInfo = await _chatClient.GetModelInfoAsync();
                            AddServerLog($"Model info: {modelInfo}");

                            // Fetch detailed model information
                            AddServerLog("Fetching detailed model metadata...");
                            CurrentModelInfo = await _chatClient.GetDetailedModelInfoAsync();
                            if (CurrentModelInfo != null)
                            {
                                AddServerLog($"Model loaded: {CurrentModelInfo.Name ?? CurrentModelInfo.Id}");
                                if (CurrentModelInfo.Meta != null)
                                {
                                    AddServerLog($"  Architecture: {CurrentModelInfo.Architecture}");
                                    AddServerLog($"  Parameters: {FormatParameterCount(CurrentModelInfo.Meta.ParameterCount)}");
                                    AddServerLog($"  Vocabulary: {CurrentModelInfo.Meta.VocabSize:N0} tokens");
                                    AddServerLog($"  Training Context: {CurrentModelInfo.Meta.TrainingContextLength:N0} tokens");
                                    AddServerLog($"  Embedding Size: {CurrentModelInfo.Meta.EmbeddingDimensions}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            AddServerLog($"Failed to get model info: {ex.Message}");
                        }

                        try
                        {
                            AddServerLog("Testing chat completions endpoint...");
                            var testResult = await _chatClient.TestChatEndpointAsync();
                            AddServerLog($"Chat endpoint test result:\n{testResult}");
                        }
                        catch (Exception ex)
                        {
                            AddServerLog($"Failed to test chat endpoint: {ex.Message}");
                        }

                        AddServerLog("=== Diagnostics Complete ===");
                    }
                    else
                    {
                        StatusMessage = "Server started but health check failed. Check logs.";
                        AddServerLog("WARNING: Health check failed");
                    }
                }
                else
                {
                    StatusMessage = "Failed to start server. Check logs.";
                    AddServerLog("ERROR: Server startup failed");
                }

                AddServerLog("=== StartServerAsync finished ===");
            }
            catch (Exception ex)
            {
                var errorMsg = $"FATAL ERROR in StartServerAsync: {ex.GetType().Name} - {ex.Message}\nStack trace: {ex.StackTrace}";
                AddServerLog(errorMsg);
                StatusMessage = $"Fatal error: {ex.Message}";
                MessageBox.Show($"An error occurred while starting the server:\n\n{ex.Message}\n\nCheck the logs for more details.", "Server Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void StopServer()
        {
            _serverManager?.StopServer();
            _chatClient = null;
            CurrentModelInfo = null;
            IsServerRunning = false;
            IsServerReady = false;
            StatusMessage = "Server stopped";
        }

        private async Task SendMessageAsync()
        {
            if (_chatClient == null || string.IsNullOrWhiteSpace(UserInput))
                return;

            IsBusy = true;

            var userMessage = new ChatMessage
            {
                Role = "user",
                Content = UserInput,
                Timestamp = DateTime.Now
            };

            ChatMessages.Add(userMessage);

            var assistantMessage = new ChatMessage
            {
                Role = "assistant",
                Content = string.Empty,
                Timestamp = DateTime.Now
            };

            ChatMessages.Add(assistantMessage);

            UserInput = string.Empty;

            // Get only messages with content for the API call
            var messages = ChatMessages.Where(m => !string.IsNullOrEmpty(m.Content)).ToList();

            AddServerLog($"[Chat] Sending {messages.Count} message(s) to server at {_chatClient.GetBaseUrl()}");
            AddServerLog($"[Chat] Last user message: {userMessage.Content.Substring(0, Math.Min(50, userMessage.Content.Length))}...");

            try
            {
                var response = await _chatClient.SendChatMessageAsync(messages, stream: true);
                AddServerLog($"[Chat] Request completed. Response length: {response?.Length ?? 0} characters");

                // If streaming didn't populate the message (no chunks received), check if we got a response
                var lastMessage = ChatMessages.LastOrDefault(m => m.Role == "assistant");
                if (lastMessage != null && string.IsNullOrEmpty(lastMessage.Content) && !string.IsNullOrEmpty(response))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // ChatMessage now implements INotifyPropertyChanged, so the UI will update automatically
                        lastMessage.Content = response;
                    });
                }
                else if (lastMessage != null && string.IsNullOrEmpty(lastMessage.Content))
                {
                    // No response received, remove the empty assistant message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ChatMessages.Remove(lastMessage);
                    });
                    AddServerLog("Warning: No response received from server. Check server logs for errors.");
                }
            }
            catch (Exception ex)
            {
                // Remove the empty assistant message on error
                var lastMessage = ChatMessages.LastOrDefault(m => m.Role == "assistant");
                if (lastMessage != null && string.IsNullOrEmpty(lastMessage.Content))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ChatMessages.Remove(lastMessage);
                    });
                }
                AddServerLog($"Error sending message: {ex.Message}");
            }

            IsBusy = false;
        }

        private void OnResponseChunkReceived(object? sender, string chunk)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var lastMessage = ChatMessages.LastOrDefault(m => m.Role == "assistant");
                if (lastMessage != null)
                {
                    // ChatMessage now implements INotifyPropertyChanged, so the UI will update automatically
                    lastMessage.Content += chunk;
                }
            });
        }

        private void BrowseModel()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "GGUF Models (*.gguf)|*.gguf|All Files (*.*)|*.*",
                Title = "Select a Model File"
            };

            if (dialog.ShowDialog() == true)
            {
                ModelPath = dialog.FileName;
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            IsBusy = true;
            StatusMessage = "Checking for updates...";

            var release = await _githubService.GetLatestReleaseAsync();

            if (release != null)
            {
                StatusMessage = $"Latest version: {release.TagName} (Published: {release.PublishedAt:yyyy-MM-dd})";

                // Update all downloadable variants with latest version and installed version
                foreach (var downloadableVariant in DownloadableVariants)
                {
                    downloadableVariant.LatestVersion = release.TagName;
                    downloadableVariant.InstalledVersion = _githubService.GetInstalledVersion(downloadableVariant.Variant) ?? "Not installed";

                    // Check if assets are available for this variant
                    var assets = await _githubService.GetAvailableAssetsForVariantAsync(downloadableVariant.Variant);
                    if (assets.Count == 0)
                    {
                        downloadableVariant.StatusMessage = "Not available for this platform";
                    }
                    else
                    {
                        downloadableVariant.StatusMessage = downloadableVariant.InstalledVersion == release.TagName
                            ? "Up to date"
                            : "Update available";
                    }
                }

                MessageBox.Show($"Latest version: {release.TagName}\n\nPublished: {release.PublishedAt:yyyy-MM-dd}\n\nSelect variants and click Download to install.", "Update Check Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = "Failed to check for updates.";
            }

            IsBusy = false;
        }

        private async Task DownloadSelectedVariantAsync()
        {
            // Get all selected variants
            var selectedVariants = DownloadableVariants.Where(v => v.IsSelected).ToList();

            if (selectedVariants.Count == 0)
            {
                StatusMessage = "Please select at least one variant to download.";
                MessageBox.Show("Please select at least one variant to download.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Check if server is running with one of the selected variants
            if (IsServerRunning && SelectedVariant != null)
            {
                var runningVariant = selectedVariants.FirstOrDefault(v => v.Variant.Type == SelectedVariant.Type);
                if (runningVariant != null)
                {
                    StatusMessage = "Cannot download variant while server is running with it.";
                    MessageBox.Show($"The server is currently running with {runningVariant.Variant.DisplayName}.\n\nPlease stop the server before downloading this variant.",
                        "Server Running", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            IsDownloading = true;
            IsBusy = true;

            var successCount = 0;
            var failCount = 0;

            foreach (var downloadableVariant in selectedVariants)
            {
                try
                {
                    downloadableVariant.IsDownloading = true;
                    downloadableVariant.DownloadProgress = 0;
                    downloadableVariant.StatusMessage = "Finding download...";
                    StatusMessage = $"Downloading {downloadableVariant.Variant.DisplayName}...";

                    var assets = await _githubService.GetAvailableAssetsForVariantAsync(downloadableVariant.Variant);

                    if (assets.Count == 0)
                    {
                        downloadableVariant.StatusMessage = "Download not found";
                        failCount++;
                        continue;
                    }

                    var asset = assets.First();
                    downloadableVariant.StatusMessage = $"Downloading {asset.Name}...";

                    // Subscribe to progress updates for this specific download
                    EventHandler<DownloadProgressEventArgs> progressHandler = (s, e) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            downloadableVariant.DownloadProgress = e.ProgressPercentage;
                        });
                    };

                    _githubService.DownloadProgressChanged += progressHandler;

                    var success = await _githubService.DownloadAndInstallAsync(asset, downloadableVariant.Variant);

                    _githubService.DownloadProgressChanged -= progressHandler;

                    if (success)
                    {
                        downloadableVariant.StatusMessage = "Installed successfully";
                        downloadableVariant.InstalledVersion = _githubService.GetInstalledVersion(downloadableVariant.Variant) ?? "Not installed";
                        downloadableVariant.DownloadProgress = 100;
                        successCount++;

                        // Update server settings if this is the selected variant
                        if (SelectedVariant?.Type == downloadableVariant.Variant.Type)
                        {
                            OnPropertyChanged(nameof(InstalledVersion));
                        }
                    }
                    else
                    {
                        downloadableVariant.StatusMessage = "Download failed";
                        failCount++;
                    }
                }
                catch (Exception ex)
                {
                    downloadableVariant.StatusMessage = $"Error: {ex.Message}";
                    AddServerLog($"Error downloading {downloadableVariant.Variant.DisplayName}: {ex.Message}");
                    failCount++;
                }
                finally
                {
                    downloadableVariant.IsDownloading = false;
                }
            }

            IsDownloading = false;
            IsBusy = false;

            // Show summary
            var message = $"Download complete!\n\nSuccessful: {successCount}\nFailed: {failCount}";
            StatusMessage = $"Downloads complete: {successCount} succeeded, {failCount} failed";
            MessageBox.Show(message, "Download Complete", MessageBoxButton.OK,
                failCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        private void UpdateServerExecutablePath()
        {
            if (SelectedVariant != null)
            {
                var execPath = _githubService.GetServerExecutablePath(SelectedVariant);
                AddServerLog($"Server executable path: {execPath}");
            }
        }

        private void AddServerLog(string message)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";

            // Fallback logging to console/debug output
            System.Diagnostics.Debug.WriteLine(logEntry);
            Console.WriteLine(logEntry);

            try
            {
                if (Application.Current?.Dispatcher != null)
                {
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        // Already on UI thread
                        ServerLogs.Add(logEntry);

                        // Keep only last 1000 logs
                        while (ServerLogs.Count > 1000)
                        {
                            ServerLogs.RemoveAt(0);
                        }
                    }
                    else
                    {
                        // Need to invoke on UI thread
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ServerLogs.Add(logEntry);

                            // Keep only last 1000 logs
                            while (ServerLogs.Count > 1000)
                            {
                                ServerLogs.RemoveAt(0);
                            }
                        });
                    }
                }
                else
                {
                    Console.WriteLine("ERROR: Application.Current or Dispatcher is null!");
                    System.Diagnostics.Debug.WriteLine("ERROR: Application.Current or Dispatcher is null!");
                }
            }
            catch (Exception ex)
            {
                // If logging to UI fails, at least log to console/debug
                var errorMsg = $"ERROR in AddServerLog: {ex.Message}\n{ex.StackTrace}";
                Console.WriteLine(errorMsg);
                System.Diagnostics.Debug.WriteLine(errorMsg);
            }
        }

        private void AutoDetectThreads()
        {
            var processorCount = Environment.ProcessorCount;
            Threads = processorCount;
            BatchThreads = processorCount;
            AddServerLog($"Auto-detected {processorCount} CPU threads");
            StatusMessage = $"Threads set to {processorCount} (CPU cores)";
        }

        private string FormatParameterCount(long paramCount)
        {
            if (paramCount >= 1_000_000_000)
                return $"{paramCount / 1_000_000_000.0:F1}B";
            if (paramCount >= 1_000_000)
                return $"{paramCount / 1_000_000.0:F1}M";
            if (paramCount >= 1_000)
                return $"{paramCount / 1_000.0:F1}K";
            return paramCount.ToString();
        }

        private void LoadSettings()
        {
            try
            {
                var settings = _settingsService.LoadSettings();

                // Load server configuration
                if (settings.ServerConfig != null)
                {
                    _modelPath = settings.ServerConfig.ModelPath;
                    _host = settings.ServerConfig.Host;
                    _port = settings.ServerConfig.Port;
                    _contextSize = settings.ServerConfig.ContextSize;
                    _threads = settings.ServerConfig.Threads;
                    _gpuLayers = settings.ServerConfig.GpuLayers;
                    _batchSize = settings.ServerConfig.BatchSize;
                    _batchThreads = settings.ServerConfig.BatchThreads;
                    _parallelSlots = settings.ServerConfig.ParallelSlots;
                    _continuousBatching = settings.ServerConfig.ContinuousBatching;
                    _memoryLock = settings.ServerConfig.MemoryLock;
                    _disableMemoryMapping = settings.ServerConfig.DisableMemoryMapping;
                    _modelAlias = settings.ServerConfig.ModelAlias;
                    _apiKey = settings.ServerConfig.ApiKey;
                    _timeout = settings.ServerConfig.Timeout;
                    _enableEmbeddings = settings.ServerConfig.EnableEmbeddings;
                    _additionalArgs = settings.ServerConfig.AdditionalArgs;
                    _systemPrompt = settings.ServerConfig.SystemPrompt;

                    // Update Config object
                    Config.ModelPath = _modelPath;
                    Config.Host = _host;
                    Config.Port = _port;
                    Config.ContextSize = _contextSize;
                    Config.Threads = _threads;
                    Config.GpuLayers = _gpuLayers;
                    Config.BatchSize = _batchSize;
                    Config.BatchThreads = _batchThreads;
                    Config.ParallelSlots = _parallelSlots;
                    Config.ContinuousBatching = _continuousBatching;
                    Config.MemoryLock = _memoryLock;
                    Config.DisableMemoryMapping = _disableMemoryMapping;
                    Config.ModelAlias = _modelAlias;
                    Config.ApiKey = _apiKey;
                    Config.Timeout = _timeout;
                    Config.EnableEmbeddings = _enableEmbeddings;
                    Config.AdditionalArgs = _additionalArgs;
                    Config.SystemPrompt = _systemPrompt;

                    // Notify UI of changes
                    OnPropertyChanged(nameof(ModelPath));
                    OnPropertyChanged(nameof(Host));
                    OnPropertyChanged(nameof(Port));
                    OnPropertyChanged(nameof(ContextSize));
                    OnPropertyChanged(nameof(Threads));
                    OnPropertyChanged(nameof(GpuLayers));
                    OnPropertyChanged(nameof(BatchSize));
                    OnPropertyChanged(nameof(BatchThreads));
                    OnPropertyChanged(nameof(ParallelSlots));
                    OnPropertyChanged(nameof(ContinuousBatching));
                    OnPropertyChanged(nameof(MemoryLock));
                    OnPropertyChanged(nameof(DisableMemoryMapping));
                    OnPropertyChanged(nameof(ModelAlias));
                    OnPropertyChanged(nameof(ApiKey));
                    OnPropertyChanged(nameof(Timeout));
                    OnPropertyChanged(nameof(EnableEmbeddings));
                    OnPropertyChanged(nameof(AdditionalArgs));
                    OnPropertyChanged(nameof(SystemPrompt));
                }

                // Load selected variant
                if (settings.SelectedVariantType.HasValue)
                {
                    var variant = AvailableVariants.FirstOrDefault(v => v.Type == settings.SelectedVariantType.Value);
                    if (variant != null)
                    {
                        _selectedVariant = variant;
                        OnPropertyChanged(nameof(SelectedVariant));
                        OnPropertyChanged(nameof(InstalledVersion));
                    }
                }

                AddServerLog("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                AddServerLog($"Error loading settings: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    ServerConfig = new ServerConfig
                    {
                        ModelPath = _modelPath,
                        Host = _host,
                        Port = _port,
                        ContextSize = _contextSize,
                        Threads = _threads,
                        GpuLayers = _gpuLayers,
                        BatchSize = _batchSize,
                        BatchThreads = _batchThreads,
                        ParallelSlots = _parallelSlots,
                        ContinuousBatching = _continuousBatching,
                        MemoryLock = _memoryLock,
                        DisableMemoryMapping = _disableMemoryMapping,
                        ModelAlias = _modelAlias,
                        ApiKey = _apiKey,
                        Timeout = _timeout,
                        EnableEmbeddings = _enableEmbeddings,
                        AdditionalArgs = _additionalArgs,
                        SystemPrompt = _systemPrompt
                    },
                    SelectedVariantType = _selectedVariant?.Type
                };

                _settingsService.SaveSettings(settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private void ToggleTheme()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsDarkTheme = !IsDarkTheme;

                // Get the main window to force a visual update
                var mainWindow = Application.Current.MainWindow;

                if (IsDarkTheme)
                {
                    // Dark Theme Colors
                    ApplyTheme(
                        backgroundPrimary: "#0A0E1A",
                        backgroundSecondary: "#12182B",
                        backgroundTertiary: "#1A2236",
                        backgroundCard: "#1E2840",
                        backgroundCardHover: "#252F4A",
                        textPrimary: "#E8EAED",
                        textSecondary: "#9AA0B4",
                        textTertiary: "#6B7280",
                        borderPrimary: "#2D3748"
                    );

                    StatusMessage = "Switched to Dark Theme";
                }
                else
                {
                    // Light Theme Colors
                    ApplyTheme(
                        backgroundPrimary: "#F8FAFC",
                        backgroundSecondary: "#FFFFFF",
                        backgroundTertiary: "#F1F5F9",
                        backgroundCard: "#FFFFFF",
                        backgroundCardHover: "#F1F5F9",
                        textPrimary: "#1E293B",
                        textSecondary: "#475569",
                        textTertiary: "#94A3B8",
                        borderPrimary: "#E2E8F0"
                    );

                    StatusMessage = "Switched to Light Theme";
                }

                // Force the window to update its visual tree
                if (mainWindow != null)
                {
                    mainWindow.Background = (System.Windows.Media.Brush)Application.Current.Resources["BackgroundPrimary"];
                    mainWindow.InvalidateVisual();
                    mainWindow.UpdateLayout();
                }
            });
        }

        private void ApplyTheme(string backgroundPrimary, string backgroundSecondary, string backgroundTertiary,
            string backgroundCard, string backgroundCardHover, string textPrimary, string textSecondary,
            string textTertiary, string borderPrimary)
        {
            var resources = Application.Current.Resources;

            UpdateResourceColor(resources, "BackgroundPrimary", backgroundPrimary);
            UpdateResourceColor(resources, "BackgroundSecondary", backgroundSecondary);
            UpdateResourceColor(resources, "BackgroundTertiary", backgroundTertiary);
            UpdateResourceColor(resources, "BackgroundCard", backgroundCard);
            UpdateResourceColor(resources, "BackgroundCardHover", backgroundCardHover);

            UpdateResourceColor(resources, "TextPrimary", textPrimary);
            UpdateResourceColor(resources, "TextSecondary", textSecondary);
            UpdateResourceColor(resources, "TextTertiary", textTertiary);

            UpdateResourceColor(resources, "BorderPrimary", borderPrimary);
        }

        private void UpdateResourceColor(ResourceDictionary resources, string key, string colorHex)
        {
            if (resources.Contains(key))
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
                var brush = new System.Windows.Media.SolidColorBrush(color);
                brush.Freeze(); // Freeze for better performance
                resources[key] = brush;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
