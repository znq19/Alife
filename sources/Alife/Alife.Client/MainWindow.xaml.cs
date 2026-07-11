using System.Diagnostics;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Alife;

public partial class MainWindow
{
    Forms.NotifyIcon? trayIcon;
    bool isExiting;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        var icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule!.FileName);
        trayIcon = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "Alife",
            Visible = true
        };
        trayIcon.DoubleClick += TrayIcon_DoubleClick;

        var contextMenu = new Forms.ContextMenuStrip();
        contextMenu.Items.Add("显示窗口", null, (_, _) => ShowWindow());
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (_, _) => ExitApp());
        trayIcon.ContextMenuStrip = contextMenu;
    }

    void MinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    void MaximizeClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaxBtn.Content = "▢";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaxBtn.Content = "❐";
        }
    }

    void CloseClick(object sender, RoutedEventArgs e)
    {
        HideToTray();
    }

    void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
    }

    void ShowWindow()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    void ExitApp()
    {
        isExiting = true;
        trayIcon!.Visible = false;
        trayIcon.Dispose();
        trayIcon = null;
        System.Windows.Application.Current.Shutdown();
    }

    void TrayIcon_DoubleClick(object? sender, EventArgs e)
    {
        ShowWindow();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!isExiting)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (trayIcon != null)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayIcon = null;
        }
        base.OnClosed(e);
    }
}
