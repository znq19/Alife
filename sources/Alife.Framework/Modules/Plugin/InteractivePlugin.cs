using Alife.Framework;
using Microsoft.SemanticKernel;

public class InteractivePlugin : Plugin
{
    public ChatBot ChatBot => chatBot;

    public override Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        chatBot = chatActivity.ChatBot;
        return Task.CompletedTask;
    }

    ChatBot chatBot = null!;
}

public class InteractivePlugin<T> : InteractivePlugin
{
    protected void Poke(string message)
    {
        ChatBot.Poke($"[{nameof(T)}] {message}");
    }
    protected void Chat(string message)
    {
        ChatBot.Chat($"[{nameof(T)}] {message}");
    }
    protected Task ChatAsync(string message)
    {
        return ChatBot.ChatAsync($"[{nameof(T)}] {message}");
    }
}
