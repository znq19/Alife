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
