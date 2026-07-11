using AuraCoursePlanner.ViewModels;
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

namespace AuraCoursePlanner.Services;

/// <summary>
/// Keeps AuraCourse Planner running quietly in the system tray, and periodically
/// checks whether any course scheduled for today hasn't been checked in yet,
/// surfacing a balloon-tip reminder when so.
///
/// Requires the project to have &lt;UseWindowsForms&gt;true&lt;/UseWindowsForms&gt;
/// in its .csproj (needed for System.Windows.Forms.NotifyIcon — WPF has no
/// built-in tray icon API).
/// </summary>
public sealed class TrayNotificationService : IDisposable
{
    private readonly NotifyIcon _trayIcon;
    private readonly DispatcherTimer _reminderTimer;
    private readonly MainViewModel _viewModel;
    private readonly Window _mainWindow;

    private DateTime _lastCheckedDate = DateTime.MinValue;
    private bool _reminderShownToday;
    private bool _isExiting;

    public TrayNotificationService(Window mainWindow, MainViewModel viewModel, string iconPath)
    {
        _mainWindow = mainWindow;
        _viewModel = viewModel;

        _trayIcon = new NotifyIcon
        {
            Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application,
            Visible = true,
            Text = "AuraCourse Planner"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open AuraCourse Planner", null, (_, _) => ShowWindow());
        menu.Items.Add("Check Today's Progress", null, (_, _) => CheckAndNotify(force: true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowWindow();

        _mainWindow.Closing += MainWindow_Closing;
        _mainWindow.StateChanged += MainWindow_StateChanged;

        // Check every 30 minutes; also fires once shortly after startup.
        _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _reminderTimer.Tick += (_, _) => CheckAndNotify(force: false);
        _reminderTimer.Start();

        CheckAndNotify(force: false);
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        // Minimizing the window also drops it to the tray instead of the taskbar.
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.Hide();
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExiting) return;

        // Closing the window (the X button) minimizes to tray instead of quitting,
        // so the reminder timer keeps running in the background. Use the tray
        // menu's "Exit" to actually quit.
        e.Cancel = true;
        _mainWindow.Hide();
    }

    private void ShowWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApp()
    {
        _isExiting = true;
        Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>Shows a balloon reminder once per day (or immediately when forced
    /// from the tray menu) listing courses scheduled for today that have no
    /// study session logged yet.</summary>
    private void CheckAndNotify(bool force)
    {
        if (_lastCheckedDate.Date != DateTime.Today)
        {
            _lastCheckedDate = DateTime.Today;
            _reminderShownToday = false;
        }

        if (!force && _reminderShownToday) return;

        var pending = _viewModel.Courses
            .Where(c => !c.IsCompleted && c.IsTodayScheduled && !c.IsTodayCheckedIn)
            .Select(c => c.Title)
            .ToList();

        if (pending.Count == 0)
        {
            if (force)
            {
                _trayIcon.ShowBalloonTip(4000, "AuraCourse Planner",
                    "You're all caught up for today. 🎉", ToolTipIcon.Info);
            }
            return;
        }

        var message = pending.Count == 1
            ? $"Today's goal: {pending[0]}"
            : $"{pending.Count} courses need a check-in today: {string.Join(", ", pending.Take(3))}{(pending.Count > 3 ? "…" : "")}";

        _trayIcon.ShowBalloonTip(6000, "AuraCourse Planner — Today's To-Do", message, ToolTipIcon.Info);
        _reminderShownToday = true;
    }

    public void Dispose()
    {
        _reminderTimer.Stop();
        _mainWindow.Closing -= MainWindow_Closing;
        _mainWindow.StateChanged -= MainWindow_StateChanged;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }
}