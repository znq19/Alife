using Newtonsoft.Json;

namespace Alife.Function.Memory;

/// <summary>
/// 基于层级文件夹的 JSON 存储。
/// 路径结构：{root}/Memory/{characterId}/L{level}/{id}.json
/// </summary>
// [/] 重构 `MemoryStorage.cs`
// - [x] 实现 `SaveFrontier` / `LoadFrontier` (L0-Ln 活跃层快照)
// - [x] 实现 `SaveArchive` / `LoadArchive` (压缩批次持久化)
public class MemoryStorage
{
    public MemoryStorage(string rootPath)
    {
        this.rootPath = rootPath;
    }

    public void SaveFrontier(string characterId, Dictionary<int, List<MemoryRecord>> activeLevels)

    {
        string path = GetFrontierPath(characterId);
        string? dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        File.WriteAllText(path, JsonConvert.SerializeObject(activeLevels, Formatting.Indented));
    }

    public Dictionary<int, List<MemoryRecord>>? LoadFrontier(string characterId)
    {
        string path = GetFrontierPath(characterId);
        if (!File.Exists(path)) return null;

        return JsonConvert.DeserializeObject<Dictionary<int, List<MemoryRecord>>>(File.ReadAllText(path));
    }

    public void SaveArchive(string characterId, string summaryId, List<MemoryRecord> batch)
    {
        string dir = Path.Combine(rootPath, "Memory", characterId, "Archives");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string path = Path.Combine(dir, $"{summaryId}.json");
        File.WriteAllText(path, JsonConvert.SerializeObject(batch, Formatting.Indented));
    }

    public List<MemoryRecord>? LoadArchive(string characterId, string summaryId)
    {
        string path = Path.Combine(rootPath, "Memory", characterId, "Archives", $"{summaryId}.json");
        if (!File.Exists(path)) return null;

        return JsonConvert.DeserializeObject<List<MemoryRecord>>(File.ReadAllText(path));
    }

    readonly string rootPath;

    string GetFrontierPath(string characterId) => 
        Path.Combine(rootPath, "Memory", characterId, "Frontier.json");
}

