using System.Runtime.InteropServices;

namespace Alife.Basic;

public static class AlifeDevice
{
    public static (int Width, int Height) GetResolution()
    {
        IntPtr hdc = GetDC(IntPtr.Zero); // 获取屏幕设备上下文
        try
        {
            int width = GetDeviceCaps(hdc, DESKTOPHORZRES);
            int height = GetDeviceCaps(hdc, DESKTOPVERTRES);
            return (width, height);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, hdc); // 必须释放句柄
        }
    }

    const int DESKTOPHORZRES = 118;
    const int DESKTOPVERTRES = 117;

    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")]
    static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
}
