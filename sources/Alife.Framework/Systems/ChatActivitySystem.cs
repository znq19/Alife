namespace Alife.Framework;

public class ChatActivitySystem
{
    public IEnumerable<ChatActivity> GetAllChatActivities()
    {
        return activities.Values;
    }

    public bool IsActivated(Character character)
    {
        return activities.ContainsKey(character.Name);
    }

    public async Task Play(Character character, IProgress<(string, float)>? progress = null)
    {
        ChatActivity chatActivity = await ChatActivity.Create(character, configuration, pluginSystem, progress, [
            configuration,
            storageSystem,
            pluginSystem
        ]);
        activities.Add(character.Name, chatActivity);
    }

    public async Task Stop(Character character)
    {
        ChatActivity chatActivity = activities[character.Name];
        await chatActivity.DisposeAsync();
        activities.Remove(character.Name);
    }

    public ChatActivitySystem(ConfigurationSystem configuration, StorageSystem storageSystem, PluginSystem pluginSystem)
    {
        this.configuration = configuration;
        this.storageSystem = storageSystem;
        this.pluginSystem = pluginSystem;
    }

    readonly ConfigurationSystem configuration;
    readonly StorageSystem storageSystem;
    readonly PluginSystem pluginSystem;
    readonly Dictionary<string, ChatActivity> activities = new();
}
