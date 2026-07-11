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
            Services = App.ServiceProvider
        };
        blazor.RootComponents.Add<Components.Routes>("#app");
        Controls.Add(blazor);

        // Form设置
        Text = "Alife";
        Icon = icon;
        ClientSize = new Size(1264, 681);
        StartPosition = FormStartPosition.CenterScreen;
        
        ResumeLayout(false);
    }
    void InitializeTrayIcon(Icon icon)
    {
        trayIcon = new NotifyIcon();
        trayIcon.Text = "Alife";
        trayIcon.Icon = icon;

        trayIcon.Click += (_, _) => ShowWindow();

        ContextMenuStrip trayMenu = new();
        trayMenu.Items.Add("显示主窗口", null, (_, _) => ShowWindow());
        trayMenu.Items.Add("退出", null, (_, _) => { Application.Exit(); });
        trayIcon.ContextMenuStrip = trayMenu;

        trayIcon.Visible = true;
    }
    void ShowWindow()
    {
        ShowInTaskbar = true;
        Show();
        Activate();
    }
    void HintWindow()
    {
        ShowInTaskbar = false;
        Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            DialogResult result = MessageBox.Show(
                "是 - 最小化到托盘\n否 - 直接退出",
                "是否最小化到托盘？",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                HintWindow();
            }
            else if (result == DialogResult.No)
            {
                Application.Exit();
            }

            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }
}
