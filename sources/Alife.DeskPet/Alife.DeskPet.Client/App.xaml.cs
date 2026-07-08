using System;
using System.IO;
using System.Text;
using System.Windows;
using Alife.Function.DeskPet;

namespace Alife.DeskPet;

public partial class App
{
    PetEngine engine = null!;

    protected override async void OnStartup(StartupEventArgs startupEvent)
    {
        try
        {
            Console.InputEncoding = new UTF8Encoding(false);
            Console.OutputEncoding = new UTF8Encoding(false);
            File.Create("pet.log").Close();
            base.OnStartup(startupEvent);

            engine = await PetEngine.Create(Environment.GetCommandLineArgs());
            MainWindow = engine.MainWindow;
        }
        catch (Exception e)
        {
            await File.AppendAllTextAsync("pet.log", e + Environment.NewLine);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        engine.DisposeAsync().AsTask().Wait();
        base.OnExit(e);
    }
}
