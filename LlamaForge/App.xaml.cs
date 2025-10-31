using System.Windows;
using LlamaForge.Services;

namespace LlamaForge
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set shutdown mode to prevent app from closing when startup screen closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

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

            // Show the main window and set it as the main application window
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            // Now switch shutdown mode to close when main window closes
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            mainWindow.Show();
        }
    }
}
