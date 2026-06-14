using Alife.Platform;

namespace Alife.PluginMarket;

public class PipEnvironmentInstaller : IEnvironmentInstaller
{
    static bool _setuptoolsReady;

    public void InstallEnvironment(IEnumerable<KeyValuePair<string, string>> environment)
    {
        if (!_setuptoolsReady)
        {
            AlifePlatform.Command("python", "-m pip install setuptools wheel --quiet");
            _setuptoolsReady = true;
        }

        string tempFile = Path.Combine(Path.GetTempPath(), "PipEnvironmentInstaller_requirements.txt");
        File.WriteAllLines(
            tempFile,
            environment.Select(dep => $"{dep.Key}{dep.Value}")
        );

        try
        {
            AlifePlatform.Command("python", $"-m pip install -r {tempFile}");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
