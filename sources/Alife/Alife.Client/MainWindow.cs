using Microsoft.AspNetCore.Components.WebView.WindowsForms;

namespace Alife;

public class MainWindow : Form
{
    NotifyIcon? trayIcon;

    public MainWindow()
    {
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alife-icon.ico");
        Icon icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;

        InitializeComponent(icon);
        InitializeTrayIcon(icon);
    }
    void InitializeComponent(Icon icon)
    {
        SuspendLayout();

        // Control设置
        Name = "MainWindow";
        BlazorWebView blazor = new() {
            Dock = DockStyle.Fill,
            HostPage = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "index.html"),
            StartPath = "/",
            Services = Program.ServiceProvider
        };
        blazor.RootComponents.Add<Components.Routes>("#app");
        Controls.Add(blazor);

        // Form设置
        Text = "Alife";
        Icon = icon;
        ClientSize = new Size(1500, 950);
        StartPosition = FormStartPosition.CenterScreen;

        ResumeLayout(false);
    }
    void InitializeTrayIcon(Icon icon)
    {
        trayIcon = new NotifyIcon();
        trayIcon.Text = "Alife";
        trayIcon.Icon = icon;

        trayIcon.MouseClick += (_, e) => {
            if (e.Button == MouseButtons.Left)
            {
                if (ShowInTaskbar)
                    HideWindow();
                else
                    ShowWindow();
            }
        };

        ContextMenuStrip trayMenu = new();
        trayMenu.Items.Add("显示主窗口", null, (_, _) => ShowWindow());
        trayMenu.Items.Add("退出", null, (_, _) => { Application.Exit(); });
        trayIcon.ContextMenuStrip = trayMenu;

        trayIcon.Visible = true;
    }

    Rectangle? savedBounds;

    void ShowWindow()
    {
        Show();
        ShowInTaskbar = true;

        if (savedBounds.HasValue)
        {
            WindowState = FormWindowState.Normal;
            Bounds = savedBounds.Value;
        }
        else
        {
            WindowState = FormWindowState.Maximized;
        }

        Activate();
    }
    void HideWindow()
    {
        savedBounds = WindowState == FormWindowState.Normal ? Bounds : null;
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            DialogResult result = MessageBox.Show(
                "是 - 直接关闭\n否 - 最小化到托盘",
                "是否直接关闭应用？",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                Application.Exit();
            }
            else if (result == DialogResult.No)
            {
                HideWindow();
            }

            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }
}
