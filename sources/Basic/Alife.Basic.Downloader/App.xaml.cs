using System.Windows;
using System.Linq;
using System;

namespace Alife.Basic.Downloader;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        string title = "AI 资源下载";
        string targetDir = "";
        string filesRaw = "";

        for (int i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i] == "--title" && i + 1 < e.Args.Length) title = e.Args[++i];
            if (e.Args[i] == "--dir" && i + 1 < e.Args.Length) targetDir = e.Args[++i];
            if (e.Args[i] == "--files" && i + 1 < e.Args.Length) filesRaw = e.Args[++i];
        }

        if (string.IsNullOrEmpty(targetDir) || string.IsNullOrEmpty(filesRaw))
        {
            Shutdown();
            return;
        }

        // 2. 找到下载器 EXE 路径
        string exeName = "Alife.Basic.Downloader.exe";

        var files = filesRaw.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => {
                var parts = f.Split('|');
                return (FileName: parts[0], Url: parts[1]);
            }).ToList();

        MainWindow window = new MainWindow(title, targetDir, files);
        window.Show();
    }
}
