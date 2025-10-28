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
        private LlamaServerManager? _serverManager;
        private LlamaChatClient? _chatClient;

        // Observable Collections
        public ObservableCollection<ChatMessage> ChatMessages { get; } = new();
        public ObservableCollection<string> ServerLogs { get; } = new();
        public ObservableCollection<LlamaVariant> AvailableVariants { get; } = new();

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
            }
        }

        private string _modelPath = string.Empty;
        public string ModelPath
        {
            get => _modelPath;
            set { _modelPath = value; OnPropertyChanged(); Config.ModelPath = value; }
        }

        private string _host = "127.0.0.1";
        public string Host
        {
            get => _host;
            set { _host = value; OnPropertyChanged(); Config.Host = value; }
        }

        private int _port = 8080;
        public int Port
        {
            get => _port;
            set { _port = value; OnPropertyChanged(); Config.Port = value; }
        }

        private int _contextSize = 2048;
        public int ContextSize
        {
            get => _contextSize;
            set { _contextSize = value; OnPropertyChanged(); Config.ContextSize = value; }
        }

        private int _threads = 4;
        public int Threads
        {
            get => _threads;
            set { _threads = value; OnPropertyChanged(); Config.Threads = value; }
        }

        private int _gpuLayers = 0;
        public int GpuLayers
        {
            get => _gpuLayers;
            set { _gpuLayers = value; OnPropertyChanged(); Config.GpuLayers = value; }
        }

        private string _additionalArgs = string.Empty;
        public string AdditionalArgs
        {
            get => _additionalArgs;
            set { _additionalArgs = value; OnPropertyChanged(); Config.AdditionalArgs = value; }
        }

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

        public MainViewModel()
        {
            var installPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LlamaForge",
                "llama-cpp"
            );

            _githubService = new GitHubService(installPath);
            _githubService.DownloadProgressChanged += (s, e) => DownloadProgress = e.ProgressPercentage;
            _githubService.StatusChanged += (s, message) => AddServerLog(message);

            // Initialize available variants
            foreach (var variant in LlamaVariant.GetAvailableVariants())
            {
                AvailableVariants.Add(variant);
            }

            // Commands
            StartServerCommand = new RelayCommand(async _ => await StartServerAsync(), _ => CanStartServer);
            StopServerCommand = new RelayCommand(_ => StopServer(), _ => CanStopServer);
            SendMessageCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => CanSendMessage);
            BrowseModelCommand = new RelayCommand(_ => BrowseModel());
            CheckUpdatesCommand = new RelayCommand(async _ => await CheckForUpdatesAsync());
            DownloadVariantCommand = new RelayCommand(async _ => await DownloadSelectedVariantAsync());
            ClearChatCommand = new RelayCommand(_ => ChatMessages.Clear());
            ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());

            // Load settings
            LoadSettings();
        }

        private async Task StartServerAsync()
        {
            if (SelectedVariant == null)
            {
                StatusMessage = "Please select a variant first.";
                return;
            }

            var executablePath = _githubService.GetServerExecutablePath(SelectedVariant);

            if (!File.Exists(executablePath))
            {
                StatusMessage = $"Server executable not found. Please download {SelectedVariant.DisplayName} first.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Starting server...";

            _serverManager = new LlamaServerManager(Config, executablePath);
            _serverManager.OutputReceived += (s, log) => AddServerLog(log);
            _serverManager.ErrorReceived += (s, log) => AddServerLog($"ERROR: {log}");
            _serverManager.ServerStatusChanged += (s, running) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsServerRunning = running;
                    StatusMessage = running ? "Server is running" : "Server stopped";
                });
            };

            var success = await _serverManager.StartServerAsync();

            if (success)
            {
                _chatClient = new LlamaChatClient(Host, Port);
                _chatClient.ResponseChunkReceived += OnResponseChunkReceived;
                _chatClient.ErrorOccurred += (s, error) => AddServerLog($"Chat Error: {error}");

                // Wait for server to be ready
                await Task.Delay(3000);

                var isHealthy = await _chatClient.CheckHealthAsync();
                if (isHealthy)
                {
                    StatusMessage = "Server is ready!";
                }
                else
                {
                    StatusMessage = "Server started but health check failed. Check logs.";
                }
            }
            else
            {
                StatusMessage = "Failed to start server. Check logs.";
            }

            IsBusy = false;
        }

        private void StopServer()
        {
            _serverManager?.StopServer();
            _chatClient = null;
            IsServerRunning = false;
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

            var messages = ChatMessages.Where(m => !string.IsNullOrEmpty(m.Content)).ToList();

            await _chatClient.SendChatMessageAsync(messages, stream: true);

            IsBusy = false;
        }

        private void OnResponseChunkReceived(object? sender, string chunk)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var lastMessage = ChatMessages.LastOrDefault(m => m.Role == "assistant");
                if (lastMessage != null)
                {
                    lastMessage.Content += chunk;
                    // Force UI update
                    var index = ChatMessages.IndexOf(lastMessage);
                    ChatMessages[index] = lastMessage;
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
                MessageBox.Show($"Latest version: {release.TagName}\n\nPublished: {release.PublishedAt:yyyy-MM-dd}\n\nSelect a variant and click Download to install.", "Update Available", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = "Failed to check for updates.";
            }

            IsBusy = false;
        }

        private async Task DownloadSelectedVariantAsync()
        {
            if (SelectedVariant == null)
            {
                StatusMessage = "Please select a variant first.";
                return;
            }

            IsBusy = true;
            DownloadProgress = 0;
            StatusMessage = $"Finding {SelectedVariant.DisplayName} download...";

            var assets = await _githubService.GetAvailableAssetsForVariantAsync(SelectedVariant);

            if (assets.Count == 0)
            {
                StatusMessage = $"No download found for {SelectedVariant.DisplayName}";
                MessageBox.Show($"Could not find a download for {SelectedVariant.DisplayName}.\n\nThis variant may not be available in the latest release.", "Download Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                IsBusy = false;
                return;
            }

            var asset = assets.First();
            StatusMessage = $"Downloading {asset.Name}...";

            var success = await _githubService.DownloadAndInstallAsync(asset, SelectedVariant);

            if (success)
            {
                StatusMessage = $"{SelectedVariant.DisplayName} installed successfully!";
                OnPropertyChanged(nameof(InstalledVersion));
                MessageBox.Show($"{SelectedVariant.DisplayName} has been installed successfully!", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = "Download failed. Check logs.";
            }

            IsBusy = false;
            DownloadProgress = 0;
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                ServerLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

                // Keep only last 1000 logs
                while (ServerLogs.Count > 1000)
                {
                    ServerLogs.RemoveAt(0);
                }
            });
        }

        private void LoadSettings()
        {
            // TODO: Load settings from file
            // For now, use defaults
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
