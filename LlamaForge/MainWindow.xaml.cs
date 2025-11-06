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

                // Log console messages from the WebUI for debugging
                ChatWebView.CoreWebView2.WebMessageReceived += (sender, args) =>
                {
                    System.Diagnostics.Debug.WriteLine($"WebUI Message: {args.TryGetWebMessageAsString()}");
                };

                // Add navigation error handler
                ChatWebView.CoreWebView2.NavigationCompleted += (sender, args) =>
                {
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
                            <p>This may occur if:</p>
                            <ul>
                                <li>The llama-server binary doesn't include the embedded WebUI</li>
                                <li>The server hasn't fully started yet</li>
                                <li>The server version is outdated</li>
                            </ul>
                            <p><strong>Suggestions:</strong></p>
                            <ul>
                                <li>Check the Server tab logs for errors</li>
                                <li>Ensure you've downloaded a recent version of llama.cpp (post-2024)</li>
                                <li>Try using the API directly via cURL or Postman at <code>http://{ViewModel?.Host}:{ViewModel?.Port}/v1/chat/completions</code></li>
                            </ul>
                            <button onclick='location.reload()'>Retry</button>
                        </body>
                        </html>";

                        ChatWebView.CoreWebView2.NavigateToString(errorHtml);
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
            if (e.PropertyName == nameof(MainViewModel.IsServerRunning))
            {
                UpdateWebViewUrl();
            }
        }

        private void UpdateWebViewUrl()
        {
            if (ViewModel == null || ChatWebView.CoreWebView2 == null)
                return;

            if (ViewModel.IsServerRunning)
            {
                // Navigate to llama.cpp's embedded WebUI
                // Try /index.html explicitly as some builds may not serve / correctly
                string url = $"http://{ViewModel.Host}:{ViewModel.Port}/index.html";
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
