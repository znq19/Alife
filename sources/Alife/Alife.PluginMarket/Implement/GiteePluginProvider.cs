using Alife.Platform;
using Newtonsoft.Json;

namespace Alife.PluginMarket;

public class GiteePluginProvider(string owner, string repo, string branch = "master") : IPluginProvider
{
    readonly string zipUrl = $"https://gitee.com/{owner}/{repo}/repository/archive/{branch}.zip";
    readonly string repoDir = Path.Combine(AlifePath.TempFolderPath, "PluginRepoGitee");

    public async Task<Plugin[]> GetPluginsAsync()
    {
        await FetchRepositoryAsync();
        return LoadPlugins();
    }

    async Task FetchRepositoryAsync()
    {
        if (Directory.Exists(repoDir))
            Directory.Delete(repoDir, true);

        await AlifePlatform.DownloadZipFileAsync(repoDir, zipUrl);
    }

    Plugin[] LoadPlugins()
    {
        if (!Directory.Exists(repoDir))
            return [];

        List<Plugin> plugins = new();
        foreach (string file in Directory.GetFiles(repoDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                string json = File.ReadAllText(file);
                Plugin? plugin = JsonConvert.DeserializeObject<Plugin>(json);
                if (plugin != null && !string.IsNullOrEmpty(plugin.Id))
                    plugins.Add(plugin);
            }
            catch
            {
                // 忽略解析失败的文件
            }
        }

        return plugins.ToArray();
    }
}
