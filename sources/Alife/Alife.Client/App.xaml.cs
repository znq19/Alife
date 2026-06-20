using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Alife.Framework;
using Alife.Components.Services;
using Alife.Platform;
using Microsoft.Extensions.Logging;

namespace Alife;

public partial class App
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

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

        //准备依赖注入容器
        {
            ServiceCollection services = new();
            // 基础 Blazor Desktop 支持
            services.AddWpfBlazorWebView();
            services.AddBlazorWebViewDeveloperTools();// 允许 F12
            // UI 库
            services.AddAntDesign();
            // logger 库
            services.AddLogging(builder => {
                builder.AddConsole();
                builder.AddFile(Path.Combine(AlifePath.RuntimeFolderPath, "Logs"), "app");
                builder.SetMinimumLevel(LogLevel.Information);
            });
            // Alife.Client 核心业务系统
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
        }

        // 初始化日志
        loggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
        logger = loggerFactory.CreateLogger<App>();

        // 订阅全局异常处理
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        EnvironmentInstaller.SetupEnvironmentPaths();
        MirrorProvider.SetupEnvironment();

        //开始应用
        ServiceProvider.GetRequiredService<ChatMessageService>();
        ServiceProvider.GetRequiredService<PluginMarketService>();
        ServiceProvider.GetRequiredService<MainWindow>().Show();
    }

    static ILoggerFactory loggerFactory = null!;
    static ILogger<App> logger = null!;

    void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        HandleUnhandledException(e.Exception, "UI线程异常");
        e.Handled = true;
    }

    void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            HandleUnhandledException(ex, "非UI线程异常");
        }
    }

    void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleUnhandledException(e.Exception, "任务异常");
        e.SetObserved();
    }

    void HandleUnhandledException(Exception exception, string source)
    {
        try
        {
            logger.LogError(exception, "未处理的异常: {Source}", source);
            loggerFactory.Dispose();
        }
        catch
        {
            // 忽略日志记录异常
        }

        try
        {
            MessageBox.Show(
                $"程序发生未处理的异常，即将退出。\n\n错误信息: {exception.Message}\n\n详细信息已记录到日志文件。",
                "Alife - 错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // 忽略消息框异常
        }

        Environment.Exit(1);
    }
}
