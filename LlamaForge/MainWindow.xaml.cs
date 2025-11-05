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
                string url = $"http://{ViewModel.Host}:{ViewModel.Port}/";
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
