using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Alife.Function.DeskPet;

/// <summary>
/// 极薄的 UI 壳层，仅通过 IPetWindow 接口提供窗口服务
/// </summary>
public partial class MainWindow : IPetWindow
{
    public static async Task<MainWindow> Create()
    {
        MainWindow mainWindow = new MainWindow();
        mainWindow.InitializeComponent();
        mainWindow.Show();

        mainWindow.StateChanged += (_, _) => {
            //禁用窗口最大化
            if (mainWindow.WindowState == WindowState.Maximized) mainWindow.WindowState = WindowState.Normal;
        };
        mainWindow.MouseDown += (_, e) => {
            //支持拖拽移动
            if (e.LeftButton == MouseButtonState.Pressed) mainWindow.DragMove();
        };

        await mainWindow.WebView.EnsureCoreWebView2Async();

        return mainWindow;
    }

    public (double Left, double Top, double Width, double Height) GetLayout()
    {
        return (Left, Top, Width, Height);
    }
    public (double ScaleX, double ScaleY) GetDpi()
    {
        CompositionTarget? compositionTarget = PresentationSource.FromVisual(this)?.CompositionTarget;
        if (compositionTarget != null)
        {
            Matrix matrix = compositionTarget.TransformToDevice;
            return (matrix.M11, matrix.M22);
        }

        return (1.0, 1.0);
    }
    public void ProgrammaticMove(double targetX, double targetY, int durationMs)
    {
        (double ScaleX, double ScaleY) dpi = GetDpi();
        double startX = Left;
        double startY = Top;
        double endX = targetX / dpi.ScaleX - Width / 2;
        double endY = targetY / dpi.ScaleY - Height / 2;

        DoubleAnimation xAnim = new DoubleAnimation(startX, endX, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = new QuadraticEase() };
        DoubleAnimation yAnim = new DoubleAnimation(startY, endY, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = new QuadraticEase() };

        BeginAnimation(LeftProperty, xAnim);
        BeginAnimation(TopProperty, yAnim);
    }

    MainWindow() { }
}
