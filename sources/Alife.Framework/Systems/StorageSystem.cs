using Alife.Basic;
using Newtonsoft.Json;

namespace Alife.Framework;

public class StorageSystem
{
    public string[] GetFolders(string key)
    {
        string path = $"{AlifePath.StorageFolderPath}/{key}";
        if (Directory.Exists(path) == false)
            return [];
        return Directory.GetDirectories(path)
            .Select(f => Path.GetFileNameWithoutExtension(f)!)
            .ToArray();
    }
    public void DeleteKey(string key)
    {
        string path = $"{AlifePath.StorageFolderPath}/{key}";
        if (Directory.Exists(path))
            Directory.Delete(path, true);
        else if (File.Exists(path))
            File.Delete(path);
    }
    public T? GetObject<T>(string key, T? defaultValue = default, JsonSerializerSettings? settings = null)
    {
        try
        {
            string? data = GetJson(key);
            if (string.IsNullOrWhiteSpace(data))
                return defaultValue;
            return JsonConvert.DeserializeObject<T>(data, settings);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return defaultValue;
        }
    }
    public void SetObject(string key, object value, JsonSerializerSettings? settings = null)
    {
        settings ??= new JsonSerializerSettings();
        settings.Formatting = Formatting.Indented;
        string data = JsonConvert.SerializeObject(value, settings);
        SetJson(key, data);
    }
    public string? GetJson(string key, string? defaultValue = null)
    {
        return GetValue(key, "json", defaultValue);
    }
    public void SetJson(string key, string value)
    {
        SetValue(key, "json", value);
    }
    public string? GetValue(string key, string type, string? defaultValue = null)
    {
        string path = $"{AlifePath.StorageFolderPath}/{key}.{type}";
        if (File.Exists(path))
            return File.ReadAllText(path);
        return defaultValue;
    }
    public void SetValue(string key, string type, string value)
    {
        string path = $"{AlifePath.StorageFolderPath}/{key}.{type}";
        if (Directory.Exists(Path.GetDirectoryName(path)) == false)
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, value);
    }
}
