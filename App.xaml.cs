using AuraCoursePlanner.Data;
using AuraCoursePlanner.Services;
using AuraCoursePlanner.ViewModels;
using AuraCoursePlanner.Views;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows; 

namespace AuraCoursePlanner
{
    public partial class App : System.Windows.Application
    {
        private TrayNotificationService? _trayService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (_, args) =>
            {
                System.Windows.MessageBox.Show(
                    $"An unexpected error occurred:\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                    "AuraCourse Planner - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                System.Windows.MessageBox.Show($"A fatal error occurred:\n\n{ex?.Message}", "AuraCourse Planner - Fatal Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            };

            TaskScheduler.UnobservedTaskException += (_, args) => { args.SetObserved(); };

            using (var db = new AuraDbContext()) { db.Database.EnsureCreated(); }

            RegisterForWindowsStartup();

            var vm = new MainViewModel(() => new AuraDbContext());
            var mainWindow = new MainWindow(vm);

            var startMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "AuraCourse.ico");

            _trayService = new TrayNotificationService(mainWindow, vm, iconPath);

            if (startMinimized)
            {
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.ShowInTaskbar = false;
                mainWindow.Hide();
            }
            else { mainWindow.Show(); }
        }

        private static void RegisterForWindowsStartup()
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return;
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
                key?.SetValue("AuraCoursePlanner", $"\"{exePath}\" --minimized");
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayService?.Dispose();
            base.OnExit(e);
        }
    }
}