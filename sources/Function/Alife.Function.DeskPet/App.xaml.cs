using System.Windows;

namespace Alife.Function.DeskPet;

public partial class App
{
    PetActivity activity = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            MainWindow mainWindow = await Alife.Function.DeskPet.MainWindow.Create();
            PetProcess petProcess = new(Console.Out, Console.In); //与客户端的通讯
            PetBridge bridge = new(mainWindow.WebView); //与前端的通讯
            PetModelMetadata metadata = PetModelMetadata.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot/model/Mao/Mao.model3.json"));

            MainWindow = mainWindow;
            activity = new(petProcess, bridge, metadata, mainWindow);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }
}
