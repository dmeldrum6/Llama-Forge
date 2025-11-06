using System;
using System.ComponentModel;
using System.Windows;
using LlamaForge.ViewModels;

namespace LlamaForge
{
    public partial class MainWindow : Window
    {
        private MainViewModel? ViewModel => DataContext as MainViewModel;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize WebView2
            InitializeWebView();

            // Subscribe to DataContext changes
            DataContextChanged += OnDataContextChanged;
        }

        private async void InitializeWebView()
        {
            try
            {
                // Ensure WebView2 runtime is initialized
                await ChatWebView.EnsureCoreWebView2Async();

                // Enable developer tools for debugging (can be opened with F12)
                ChatWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;

                // Log all console messages from JavaScript for debugging
                ChatWebView.CoreWebView2.WebMessageReceived += (sender, args) =>
                {
                    System.Diagnostics.Debug.WriteLine($"WebUI Message: {args.TryGetWebMessageAsString()}");
                };

                // Capture console messages (console.log, console.error, etc.)
                await ChatWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                    (function() {
                        const originalLog = console.log;
                        const originalError = console.error;
                        const originalWarn = console.warn;

                        console.log = function(...args) {
                            originalLog.apply(console, args);
                            window.chrome.webview.postMessage('LOG: ' + args.join(' '));
                        };
                        console.error = function(...args) {
                            originalError.apply(console, args);
                            window.chrome.webview.postMessage('ERROR: ' + args.join(' '));
                        };
                        console.warn = function(...args) {
                            originalWarn.apply(console, args);
                            window.chrome.webview.postMessage('WARN: ' + args.join(' '));
                        };
                    })();
                ");

                // Add navigation starting handler to log URL attempts
                ChatWebView.CoreWebView2.NavigationStarting += (sender, args) =>
                {
                    System.Diagnostics.Debug.WriteLine($"WebView navigating to: {args.Uri}");
                };

                // Add navigation error handler
                ChatWebView.CoreWebView2.NavigationCompleted += (sender, args) =>
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation completed - Success: {args.IsSuccess}, URI: {args.Uri}");

                    if (!args.IsSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine($"Navigation failed: {args.WebErrorStatus}");

                        // Show error message in the WebView
                        string errorHtml = $@"
                        <html>
                        <head><title>Navigation Error</title></head>
                        <body style='font-family: Arial; padding: 20px;'>
                            <h2>Unable to load llama.cpp WebUI</h2>
                            <p><strong>Error:</strong> {args.WebErrorStatus}</p>
                            <p><strong>URL:</strong> {args.Uri}</p>
                            <p>This may occur if:</p>
                            <ul>
                                <li>The server is not serving static files correctly</li>
                                <li>The --path argument is not configured properly</li>
                                <li>The WebUI files were not downloaded successfully</li>
                            </ul>
                            <p><strong>Suggestions:</strong></p>
                            <ul>
                                <li>Check the Server tab logs for WebUI download status</li>
                                <li>Look for ""--path"" in the server command line</li>
                                <li>Try pressing F12 to open Developer Tools and check the Console tab</li>
                                <li>Test the API directly: <code>http://{ViewModel?.Host}:{ViewModel?.Port}/health</code></li>
                            </ul>
                            <button onclick='location.reload()'>Retry</button>
                        </body>
                        </html>";

                        ChatWebView.CoreWebView2.NavigateToString(errorHtml);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Navigation succeeded - WebUI should be loading");
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize WebView2: {ex.Message}\n\n" +
                    "Please ensure WebView2 Runtime is installed on your system.",
                    "WebView2 Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from old ViewModel
            if (e.OldValue is INotifyPropertyChanged oldViewModel)
            {
                oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            // Subscribe to new ViewModel
            if (e.NewValue is INotifyPropertyChanged newViewModel)
            {
                newViewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Wait for IsServerReady (after health check) instead of IsServerRunning (process start)
            // This ensures the HTTP server is fully initialized before we try to navigate
            if (e.PropertyName == nameof(MainViewModel.IsServerReady))
            {
                UpdateWebViewUrl();
            }
        }

        private void UpdateWebViewUrl()
        {
            if (ViewModel == null || ChatWebView.CoreWebView2 == null)
                return;

            if (ViewModel.IsServerReady)
            {
                // Navigate to llama.cpp's WebUI served from --path directory
                // The server automatically serves index.html at the root path
                string url = $"http://{ViewModel.Host}:{ViewModel.Port}/";
                System.Diagnostics.Debug.WriteLine($"Server is ready, navigating WebView to: {url}");
                ChatWebView.CoreWebView2.Navigate(url);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Cleanup
            if (ViewModel is INotifyPropertyChanged viewModel)
            {
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            ChatWebView.Dispose();
            base.OnClosed(e);
        }
    }
}
