using Newtonsoft.Json.Linq;

namespace Alife.Framework;

public class ConfigurationSystem
{
    public Type? GetConfigurationType(Type target)
    {
        if (configurationTypes.TryGetValue(target, out Type? configurationType))
            return configurationType;

        Type[] interfaces = target.GetInterfaces();
        Type? targetInterface = interfaces.FirstOrDefault(value => value.IsGenericType && value.GetGenericTypeDefinition() == typeof(IConfigurable<>));
        if (targetInterface == null)
            return null;

        configurationType = targetInterface.GetGenericArguments()[0];
        configurationTypes[target] = configurationType;
        return configurationType;
    }

    public bool CanConfiguration(Type type)
    {
        return GetConfigurationType(type) != null;
    }

    public object? GetConfiguration(Type target, string relativePath = "")
    {
        Type? configurationType = GetConfigurationType(target);
        if (configurationType == null)
            return null;

        string targetKey = target.FullName!;
        string currentPath = relativePath.Replace('\\', '/').Trim('/');

        while (true)
        {
            string key = string.IsNullOrEmpty(currentPath)
                ? $"Configuration/{targetKey}"
                : $"Configuration/{currentPath}/{targetKey}";

            JObject? configuration = storageSystem.GetObject<JObject>(key);
            if (configuration != null)
                return configuration.ToObject(configurationType);

            if (string.IsNullOrEmpty(currentPath))
                break;

            int lastSlash = currentPath.LastIndexOf('/');
            currentPath = (lastSlash == -1) ? "" : currentPath[..lastSlash];
        }

        return Activator.CreateInstance(configurationType, null);
    }

    public void SetConfiguration(Type target, object configuration, string relativePath = "")
    {
        if (CanConfiguration(target) == false)
            throw new Exception("目标类型不支持配置功能！");

        string currentPath = relativePath.Replace('\\', '/').Trim('/');
        string key = string.IsNullOrEmpty(currentPath)
            ? $"Configuration/{target.FullName}"
            : $"Configuration/{currentPath}/{target.FullName}";

        storageSystem.SetObject(key, configuration);
    }

    public JObject? GetConfigurationJson(Type target, string relativePath = "")
    {
        object? configuration = GetConfiguration(target, relativePath);
        if (configuration != null)
            return JObject.FromObject(configuration);
        return null;
    }

    public ConfigurationSystem(StorageSystem storageSystem)
    {
        this.storageSystem = storageSystem;
        configurationTypes = new Dictionary<Type, Type>();
    }

    readonly StorageSystem storageSystem;
    readonly Dictionary<Type, Type> configurationTypes;
}
