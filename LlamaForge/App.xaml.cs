using System.Windows;
using LlamaForge.Services;

namespace LlamaForge
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load settings to check if we should show the startup screen
            var settingsService = new SettingsService();
            var settings = settingsService.LoadSettings();

            // Show startup screen if enabled
            if (settings.ShowStartupScreen)
            {
                var startupScreen = new StartupScreen(settings, settingsService);
                var result = startupScreen.ShowDialog();

                // Only continue to main window if startup screen wasn't cancelled
                if (result != true)
                {
                    Shutdown();
                    return;
                }
            }

            // Show the main window
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
