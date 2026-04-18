using System.IO.Compression;
using System.Diagnostics;

namespace Alife.Basic;

/// <summary>
/// 管理自引导 Python 运行环境。
/// 采用 Windows Embeddable 包，确保环境隔离且即插即用。
/// </summary>
public static class AlifePython
{
    public static string FolderPath { get; }
    public static string ExecutablePath { get; }

    static AlifePython()
    {
        FolderPath = Path.Combine(AlifePath.ResourcesPath, "Python");
        ExecutablePath = Path.Combine(FolderPath, "python.exe");

        // 静态构造函数确保在任何类访问此类属性前，Python 环境已就绪
        EnsureRuntime();
    }

    private static void EnsureRuntime()
    {
        if (File.Exists(ExecutablePath)) return;

        string zipName = "python-3.11.9-embed-amd64.zip";
        string zipPath = Path.Combine(AlifePath.ResourcesPath, zipName);
        string pythonUrl = $"https://mirrors.huaweicloud.com/python/3.11.9/{zipName}";

        // 1. 下载嵌入式 Python 包
        ResourceDownloader.Ensure("Python 运行环境", AlifePath.ResourcesPath, (zipName, pythonUrl));

        // 2. 解压
        if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);
        ZipFile.ExtractToDirectory(zipPath, FolderPath, overwriteFiles: true);
        File.Delete(zipPath);

        // 3. 启用 site-packages (修改 .pth 文件)
        // 嵌入式 Python 默认不加载 site-packages，需要取消注释
        string pthFile = Path.Combine(FolderPath, "python311._pth");
        if (File.Exists(pthFile))
        {
            var lines = File.ReadAllLines(pthFile).ToList();
            bool changed = false;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim() == "#import site")
                {
                    lines[i] = "import site";
                    changed = true;
                }
            }
            if (changed) File.WriteAllLines(pthFile, lines);
        }

        // 4. 下载并安装 Pip
        string pipScript = "get-pip.py";
        string pipUrl = "https://bootstrap.pypa.io/get-pip.py";
        string pipScriptPath = Path.Combine(FolderPath, pipScript);
        
        ResourceDownloader.Ensure("Python 包管理器 (Pip)", FolderPath, (pipScript, pipUrl));

        // 运行安装脚本
        RunCommand($"\"{pipScriptPath}\"");
        File.Delete(pipScriptPath);

        // 5. 设置国内源 (阿里云)
        RunCommand("-m pip config set global.index-url https://mirrors.aliyun.com/pypi/simple/");
    }

    private static void RunCommand(string args)
    {
        ProcessStartInfo psi = new()
        {
            FileName = ExecutablePath,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = FolderPath
        };
        using var process = Process.Start(psi);
        process?.WaitForExit();
    }
}
