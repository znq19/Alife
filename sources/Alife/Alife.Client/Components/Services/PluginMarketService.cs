using System.IO;
using Alife.Framework;
using Alife.Platform;
using Alife.PluginMarket;

namespace Alife.Components.Services;

public class PluginMarketService
{
    readonly Alife.PluginMarket.PluginMarket pluginMarket;
    readonly FileSystemPluginManager localManager;
    readonly NuGetEnvironmentInstaller nugetInstaller;
    readonly ModuleSystem moduleSystem;

    public PluginMarketService(ModuleSystem moduleSystem)
    {
        this.moduleSystem = moduleSystem;

        string pluginDir = Path.Combine(AlifePath.StorageFolderPath, "Plugins");
        string installedDir = Path.Combine(AlifePath.StorageFolderPath, "Plugins_Installed");
        string packageListFile = Path.Combine(installedDir, "NUGET_PACKAGES.txt");

        Directory.CreateDirectory(pluginDir);
        Directory.CreateDirectory(installedDir);

        var onlineProvider = new GiteePluginProvider("bdffzi", "Alife.PluginMarket", "main");
        localManager = new FileSystemPluginManager(installedDir);
        nugetInstaller = new NuGetEnvironmentInstaller(packageListFile);

        Dictionary<string, IEnvironmentInstaller> environmentInstallers = new() {
            { "nuget", nugetInstaller },
            { "pip", new PipEnvironmentInstaller() }
        };

        pluginMarket = new Alife.PluginMarket.PluginMarket(onlineProvider, localManager, localManager, environmentInstallers);
    }

    public async Task InitializeAsync()
    {
        await pluginMarket.InitializeAsync();
        UpdateModuleDirectories();
    }

    void UpdateModuleDirectories()
    {
        string[] nugetPaths = nugetInstaller.ReadPackageList();
        string[] extraDirs = moduleSystem.GetExtraDirectories()
            .Where(d => !nugetPaths.Contains(d))
            .Concat(nugetPaths)
            .ToArray();
        moduleSystem.SetExtraDirectories(extraDirs);
    }

    public Plugin[] GetAllPlugins() => pluginMarket.GetAllPlugins().ToArray();

    public void RefreshLocalPlugins() => pluginMarket.RefreshLocalPlugins();

    public async Task FetchOnlinePluginsAsync() => await pluginMarket.FetchOnlinePluginsAsync();

    public Dictionary<string, string> GetInstalledPlugins() => localManager.GetPlugins();

    public Plugin? GetPlugin(string pluginId)
    {
        return GetAllPlugins().FirstOrDefault(p => p.Id == pluginId);
    }

    public bool IsInstalled(string pluginId)
    {
        return GetInstalledPlugins().ContainsKey(pluginId);
    }

    public string? GetInstalledVersion(string pluginId)
    {
        return GetInstalledPlugins().GetValueOrDefault(pluginId);
    }

    public bool HasUpdate(Plugin plugin)
    {
        string? installedVersion = GetInstalledVersion(plugin.Id);
        if (installedVersion == null || plugin.Releases == null)
            return false;

        string? latestVersion = plugin.Releases.Keys
            .OrderByDescending(v => v)
            .FirstOrDefault();

        return latestVersion != null && latestVersion != installedVersion;
    }

    public string? GetLatestVersion(Plugin plugin)
    {
        return plugin.Releases?.Keys
            .OrderByDescending(v => v)
            .FirstOrDefault();
    }

    public async Task InstallPlugin(Plugin plugin, string version)
    {
        await pluginMarket.InstallPlugin(plugin, version);
        UpdateModuleDirectories();
        try
        {
            moduleSystem.ReloadModules();
        }
        catch
        {
            await pluginMarket.UninstallPlugin(plugin);
            throw;
        }
        OnInstalled?.Invoke();
    }

    public async Task UninstallPlugin(Plugin plugin)
    {
        await pluginMarket.UninstallPlugin(plugin);
        UpdateModuleDirectories();
        moduleSystem.ReloadModules();
        OnInstalled?.Invoke();
    }

    public event Action? OnInstalled;
}
