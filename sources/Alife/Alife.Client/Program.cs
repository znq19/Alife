using System.Text;
using Alife.Framework;
using Alife.Components.Services;
using Alife.Platform;
using ElectronNET.API;
using ElectronNET.API.Entities;

namespace Alife;

public class Program
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    [STAThread]
    static void Main(string[] args)
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
        var builder = WebApplication.CreateBuilder(args);
        {
            //前端框架
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();
            //前端组件库
            builder.Services.AddAntDesign();
            //前端载体
            if (Environment.GetEnvironmentVariable("DISABLE_ElectronENT") == null)
            {
                builder.Services.AddElectron();
                builder.UseElectron(args, async () => {
                    var options = new BrowserWindowOptions {
                        Show = false,
                        IsRunningBlazor = true,
                    };
                    if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                        options.AutoHideMenuBar = true;

                    var browserWindow = await Electron.WindowManager.CreateWindowAsync(options);
                    browserWindow.OnReadyToShow += () => browserWindow.Show();
                });
            }
            //系统功能
            builder.Services.AddSingleton<StorageSystem>();
            builder.Services.AddSingleton<ConfigurationSystem>();
            builder.Services.AddSingleton<ModuleSystem>();
            builder.Services.AddSingleton<CharacterSystem>();
            builder.Services.AddSingleton<ChatActivitySystem>();
            builder.Services.AddSingleton<ChatMessageService>();
            builder.Services.AddSingleton<PluginMarketService>();
            builder.Services.AddSingleton<UpdateService>();
            builder.Services.AddSingleton<EnvironmentInstaller>();
        }

        var app = builder.Build();
        ServiceProvider = app.Services;

        Environment.SetEnvironmentVariable("COREWEBVIEW2_FORCED_HOSTING_MODE", "COREWEBVIEW2_HOSTING_MODE_WINDOW_TO_VISUAL");
        app.UseAntiforgery();
        app.UseStaticFiles();
        app.MapRazorComponents<Components.App>()
            .AddInteractiveServerRenderMode();
        app.Run();
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
