using System.Windows;
using LlamaForge.Models;
using LlamaForge.Services;

namespace LlamaForge
{
    public partial class StartupScreen : Window
    {
        private readonly AppSettings _settings;
        private readonly SettingsService _settingsService;

        public StartupScreen(AppSettings settings, SettingsService settingsService)
        {
            InitializeComponent();
            _settings = settings;
            _settingsService = settingsService;
        }

        private void GetStartedButton_Click(object sender, RoutedEventArgs e)
        {
            // Update the setting based on checkbox
            if (DontShowAgainCheckBox.IsChecked == true)
            {
                _settings.ShowStartupScreen = false;
                _settingsService.SaveSettings(_settings);
            }

            // Close the startup screen
            DialogResult = true;
            Close();
        }
    }
}
