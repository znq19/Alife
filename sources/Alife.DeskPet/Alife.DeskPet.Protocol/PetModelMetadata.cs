using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Alife.Function.DeskPet;

public record InteractionItem
{
    public string? Text { get; set; }
    public string? Exp { get; set; }
    public MotionRef? Mtn { get; set; }
}

public record MotionRef(string Group, int Index);

/// <summary>
/// Live2D 模型元数据载体与解析器
/// </summary>
public class PetModelMetadata
{
    public List<string> Expressions { get; } = new();
    public Dictionary<string, (string Group, int Index)> Motions { get; } = new();
    public Dictionary<string, List<InteractionItem>> Interactions { get; } = new();
    public string ModelPath { get; private set; } = string.Empty;

    public static string ResolveModelJsonPath(string modelRootPath, string modelName)
    {
        string model3JsonPath = Path.Combine(modelRootPath, modelName, $"{modelName}.model3.json");
        if (File.Exists(model3JsonPath)) return model3JsonPath;

        string modelJsonPath = Path.Combine(modelRootPath, modelName, $"{modelName}.model.json");
        if (File.Exists(modelJsonPath)) return modelJsonPath;

        return model3JsonPath;
    }

    public static PetModelMetadata Load(string jsonPath)
    {
        PetModelMetadata metadata = new();
        if (File.Exists(jsonPath) == false) return metadata;

        // 设置模型相对路径 (用于 Web 端加载)
        // 假设路径格式为 .../wwwroot/model/...
        int wwwrootIndex = jsonPath.Replace('\\', '/').IndexOf("wwwroot/", StringComparison.Ordinal);
        if (wwwrootIndex != -1)
        {
            metadata.ModelPath = jsonPath.Substring(wwwrootIndex + "wwwroot/".Length).Replace('\\', '/');
        }

        try
        {
            using JsonDocument jsonDoc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            JsonElement root = jsonDoc.RootElement;

            // 1. 解析表达式与动作
            JsonElement refs = root.TryGetProperty("FileReferences", out JsonElement fileRefs) ? fileRefs : root;

            // expressions（支持 Cubism3/4 大写 Name 和 Cubism2 小写 name）
            if (refs.TryGetProperty("Expressions", out JsonElement exps) || refs.TryGetProperty("expressions", out exps))
            {
                foreach (JsonElement exp in exps.EnumerateArray())
                {
                    if (exp.TryGetProperty("Name", out JsonElement nameProp) == false)
                        exp.TryGetProperty("name", out nameProp);
                    string? name = nameProp.GetString();
                    if (string.IsNullOrEmpty(name) == false) metadata.Expressions.Add(name);
                }
            }

            // motions（支持 Cubism3/4 大写 Name 和 Cubism2 小写 name/file）
            if (refs.TryGetProperty("Motions", out JsonElement motionsJson) || refs.TryGetProperty("motions", out motionsJson))
            {
                foreach (JsonProperty groupProp in motionsJson.EnumerateObject())
                {
                    string groupName = groupProp.Name;
                    int index = 0;
                    foreach (JsonElement motionItem in groupProp.Value.EnumerateArray())
                    {
                        string? motionName = null;
                        if (motionItem.TryGetProperty("Name", out JsonElement nameProp) ||
                            motionItem.TryGetProperty("name", out nameProp))
                        {
                            motionName = nameProp.GetString();
                        }
                        if (string.IsNullOrEmpty(motionName))
                        {
                            if (motionItem.TryGetProperty("File", out JsonElement fileProp) ||
                                motionItem.TryGetProperty("file", out fileProp))
                            {
                                motionName = Path.GetFileNameWithoutExtension(fileProp.GetString());
                            }
                        }
                        if (string.IsNullOrEmpty(motionName) == false) metadata.Motions[motionName] = (groupName, index);
                        index++;
                    }
                }
            }

            // 2. 解析交互配置
            if (root.TryGetProperty("Interaction", out JsonElement interactJson))
            {
                foreach (JsonProperty poolProp in interactJson.EnumerateObject())
                {
                    metadata.Interactions[poolProp.Name] = JsonSerializer.Deserialize<List<InteractionItem>>(poolProp.Value.GetRawText(), PetProcess.JsonOptions) ?? new();
                }
            }
        }
        catch
        {
            // 解析失败时保持默认空集合，符合“别没事找事”原则，不强行抛出异常
        }

        return metadata;
    }
}
