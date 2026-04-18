using System.Diagnostics;
using System.IO;

namespace Alife.Basic;

/// <summary>
/// 通用的模型与资源下载引导器。
/// 负责检测文件完整性并调用独立的 WPF 下载器窗口。
/// </summary>
public static class ResourceDownloader
{
    /// <summary>
    /// 确保指定目录下的文件完整。如果缺失，弹出独立窗口进行下载。
    /// </summary>
    /// <param name="title">任务标题</param>
    /// <param name="targetDir">目标存储目录</param>
    /// <param name="files">文件名与 URL 的映射</param>
    public static void Ensure(string title, string targetDir, params (string fileName, string url)[] files)
    {
        // 1. 检查文件是否存在
        bool allExist = true;
        foreach (var (fileName, _) in files)
        {
            if (!File.Exists(Path.Combine(targetDir, fileName)))
            {
                allExist = false;
                break;
            }
        }

        if (allExist) return;

        // 2. 找到下载器 EXE 路径
        string exeName = "Alife.Basic.Downloader.exe";
        string exePath = Path.Combine(AppContext.BaseDirectory, exeName);

        // 如果主程序目录没找到，尝试在父目录查找
        if (!File.Exists(exePath))
        {
            string? parent = Path.GetDirectoryName(AppContext.BaseDirectory);
            if (parent != null) exePath = Path.Combine(parent, exeName);
        }

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"找不到资源下载器：{exeName}，无法自动完成组件下载。");
        }

        // 3. 构建参数
        string filesArg = string.Join(";", files.Select(f => $"{f.fileName}|{f.url}"));
        string arguments = $"--title \"{title}\" --dir \"{targetDir}\" --files \"{filesArg}\"";

        // 4. 启动并阻塞等待
        ProcessStartInfo psi = new()
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = true,
        };

        using var process = Process.Start(psi);
        if (process != null)
        {
            process.WaitForExit();
        }

        // 5. 校验结果
        foreach (var (fileName, _) in files)
        {
            if (!File.Exists(Path.Combine(targetDir, fileName)))
            {
                throw new FileNotFoundException($"资源下载失败或被中断：{fileName}");
            }
        }
    }
}
