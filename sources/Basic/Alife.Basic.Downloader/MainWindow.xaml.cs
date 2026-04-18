using System.IO;
using System.Net.Http;
using System.Windows;

namespace Alife.Basic.Downloader;

public partial class MainWindow : Window
{
    private readonly string _title;
    private readonly string _targetDir;
    private readonly List<(string FileName, string Url)> _files;
    private readonly HttpClient _httpClient = new();

    public MainWindow(string title, string targetDir, List<(string FileName, string Url)> files)
    {
        InitializeComponent();
        _title = title;
        _targetDir = targetDir;
        _files = files;

        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        TxtTitle.Text = $"下载任务: {title}";
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Directory.Exists(_targetDir))
                Directory.CreateDirectory(_targetDir);

            Log($"初始化完成，准备下载 {_files.Count} 个资源...");

            foreach (var file in _files)
            {
                await DownloadFileAsync(file.FileName, file.Url);
            }

            Log("========================================");
            Log("所有任务已完成！系统即将自动继续进程...");
            await Task.Delay(2000);
            Close();
        }
        catch (Exception ex)
        {
            Log($"[错误] 处理失败: {ex.Message}");
            TxtStatus.Text = "下载过程中出现异常。";
            MessageBox.Show($"资源下载失败: {ex.Message}", "同步错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DownloadFileAsync(string fileName, string url)
    {
        // 2. 找到下载器 EXE 路径
        string exeName = "Alife.Basic.Downloader.exe";
        // 确保子目录存在
        string filePath = Path.Combine(_targetDir, fileName);
        string? subDir = Path.GetDirectoryName(filePath);
        if (subDir != null && !Directory.Exists(subDir))
            Directory.CreateDirectory(subDir);

        Log($"下载中: {fileName}");
        TxtStatus.Text = $"进程: {fileName}";

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        byte[] buffer = new byte[8192];
        long totalRead = 0;
        int read;
        var startTime = DateTime.Now;

        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read);
            totalRead += read;

            if (totalBytes.HasValue)
            {
                double percentage = (double)totalRead / totalBytes.Value * 100;
                MainProgress.Value = percentage;
                TxtPercent.Text = $"{percentage:F1}%";

                double seconds = (DateTime.Now - startTime).TotalSeconds;
                if (seconds > 0.1)
                {
                    double speed = totalRead / 1024.0 / 1024.0 / seconds;
                    TxtSpeed.Text = $"{speed:F2} MB/s";
                }
            }
        }

        Log($"已获取: {fileName} ({totalRead / 1024 / 1024} MB)");
    }

    private void Log(string message)
    {
        TxtLog.Text += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        LogScroll.ScrollToEnd();
    }
}
