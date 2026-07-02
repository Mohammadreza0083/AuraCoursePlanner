using AuraCoursePlanner.Data;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AuraCoursePlanner;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Surface any unhandled exception as a message box instead of silently
        // crashing to desktop, so problems are diagnosable during development.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "AuraCourse Planner - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true; // keep the app alive
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show($"A fatal error occurred:\n\n{ex?.Message}", "AuraCourse Planner - Fatal Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
        };

        // Ensure the SQLite database & schema exist on first run.
        using var db = new AuraDbContext();
        db.Database.EnsureCreated();
    }
}
