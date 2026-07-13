using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Alife.Framework;
using Alife.Components.Services;
using Alife.Platform;
using Microsoft.Extensions.Logging;

namespace Alife;

public class App
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    [STAThread]
    static void Main()
    {
#if DEBUG
        Console.WriteLine(typeof(Function.Memory.MemoryService).Assembly.FullName);
        Console.WriteLine(typeof(Function.MessageFilter.MessageFilterService).Assembly);
        Console.WriteLine(typeof(Function.SystemEvent.SystemEventService).Assembly);
        Console.WriteLine(typeof(Function.VirtualWorld.VirtualWorldService).Assembly);
        Console.WriteLine(typeof(Function.Developer.DeveloperService).Assembly);

        Console.WriteLine(typeof(Function.FunctionCaller.XmlFunctionCaller).Assembly);
        Console.WriteLine(typeof(Function.Mcp.McpService).Assembly);
        Console.WriteLine(typeof(Function.Skill.SkillService).Assembly);

        Console.WriteLine(typeof(Function.Browser.BrowserService).Assembly);
        Console.WriteLine(typeof(Function.Python.PythonService).Assembly);
        Console.WriteLine(typeof(Function.Vision.VisionService).Assembly);
        Console.WriteLine(typeof(Function.FileService.FileService).Assembly);
        Console.WriteLine(typeof(Function.ProcessService.ProcessService).Assembly);

        Console.WriteLine(typeof(Function.Auditory.AuditoryService).Assembly);
        Console.WriteLine(typeof(Function.DeskPet.DeskPetService).Assembly);
        Console.WriteLine(typeof(Function.QChat.QChatService).Assembly);
        Console.WriteLine(typeof(Function.Speech.SpeechService).Assembly);

        Console.WriteLine(typeof(Function.AIModelUtility.AIModelUtility).Assembly);
        Console.WriteLine(typeof(Function.Auditory.SenseVoice.SenseVoiceAuditoryModel).Assembly);
        Console.WriteLine(typeof(Function.Speech.EdgeTTS.EdgeSpeechModel).Assembly);
        Console.WriteLine(typeof(Function.Speech.Genie.GenieSpeechModel).Assembly);
        Console.WriteLine(typeof(Function.Speech.VITS.VitsSpeechModel).Assembly);
        Console.WriteLine(typeof(Function.Vision.MiniCPM.MiniCPMVisionModel).Assembly);
        Console.WriteLine(typeof(Function.Vision.OpenAI.OpenAIVisionModel).Assembly);
        Console.WriteLine(typeof(Function.Vision.Qwen.QwenVisionModel).Assembly);
#endif

        //控制台编码设置
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        //应用异常处理
        TaskScheduler.UnobservedTaskException += UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += UnhandledException;

        //业务功能注册
        ServiceCollection services = new();
        services.AddWindowsFormsBlazorWebView();
        services.AddBlazorWebViewDeveloperTools();
        services.AddAntDesign();
        services.AddLogging(builder => {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddSingleton<StorageSystem>();
        services.AddSingleton<ConfigurationSystem>();
        services.AddSingleton<ModuleSystem>();
        services.AddSingleton<CharacterSystem>();
        services.AddSingleton<ChatActivitySystem>();
        services.AddSingleton<ChatMessageService>();
        services.AddSingleton<PluginMarketService>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<EnvironmentInstaller>();
        services.AddSingleton<MainWindow>();
        ServiceProvider = services.BuildServiceProvider();

        //前端配置并启动
        Application.ThreadException += ThreadException;
        Environment.SetEnvironmentVariable("COREWEBVIEW2_FORCED_HOSTING_MODE", "COREWEBVIEW2_HOSTING_MODE_WINDOW_TO_VISUAL");//解决光标等渲染问题
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);//跟随系统 Dpi，解决字体模糊问题
        Application.EnableVisualStyles();//现代化 Form 预设组件样式
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(ServiceProvider.GetRequiredService<MainWindow>());
    }

    static void ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        HandleException(e.Exception, nameof(ThreadException), false);
    }
    static void UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleException(e.Exception, nameof(UnobservedTaskException), false);
        e.SetObserved();
    }
    static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            HandleException(ex, nameof(UnhandledException), true);
    }
    static void HandleException(Exception exception, string source, bool termination)
    {
        try
        {
            string logDir = Path.Combine(AlifePath.TempFolderPath, "Logs");
            Directory.CreateDirectory(logDir);
            string exceptionFilePath = Path.Combine(logDir, $"error-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            string content = $"""
                              发生时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}
                              异常来源: {source}

                              === 详细报错 ===
                              {exception}
                              """;
            File.WriteAllText(exceptionFilePath, content);

            string message = $"""
                              程序发生未处理的异常：
                              {exception.Message}

                              详情信息已保存至:
                              {exceptionFilePath}
                              """;

            MessageBox.Show(message, "Alife - 错误", MessageBoxButtons.OK, MessageBoxIcon.Error);

            if (termination)
                Application.Exit();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
