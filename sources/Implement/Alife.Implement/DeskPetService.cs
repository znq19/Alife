using System.ComponentModel;
using Alife.Basic;
using Alife.Framework;
using Alife.Function.DeskPet;
using Alife.Function.Interpreter;
using Microsoft.SemanticKernel;

namespace Alife.Implement;

[Plugin("Live2D桌宠", "将Live2D桌宠接入AI系统，实现表现力同步和互动反馈。")]
[Description("此服务让你获得控制Live2D桌宠以及接收其交互的能力")]
public class DeskPetService : Plugin, IAsyncDisposable
{
    [XmlFunction("say")]
    [Description("发送消息：显示一段文本字幕。示例: <say>你好</say>")]
    public async Task PetBubble(XmlExecutorContext context, [XmlContent] string content)
    {
        if (context.CallMode == CallMode.Reset)
        {
            client.HideBubble();
            return;
        }
        if (context.CallMode == CallMode.Closing)
        {
            if (DateTimeOffset.Now.ToUnixTimeMilliseconds() < lastBubbleEndTime)
                await Task.Delay(TimeSpan.FromMilliseconds(lastBubbleEndTime - DateTimeOffset.Now.ToUnixTimeMilliseconds()));
            client.HideBubble();
        }

        if (context.CallMode != CallMode.Content)
            return;

        if (string.IsNullOrWhiteSpace(content) == false)
        {
            if (DateTimeOffset.Now.ToUnixTimeMilliseconds() < lastBubbleEndTime)
                await Task.Delay(TimeSpan.FromMilliseconds(lastBubbleEndTime - DateTimeOffset.Now.ToUnixTimeMilliseconds()));
            client.ShowBubble(content);
            lastBubbleEndTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + content.Length * 150;
        }
    }
    [XmlFunction("exp")]
    [Description("控制表情：切换当前显示的表情。具体类型见补充信息。")]
    public void PetExpression(XmlExecutorContext context, string exp)
    {
        if (context.CallMode != CallMode.OneShot)
            return;
        exp = exp.Trim();
        if (string.IsNullOrWhiteSpace(exp))
            return;

        client.PlayExpression(exp);
    }
    [XmlFunction("mtn")]
    [Description("执行动作：播放预设动画。具体类型见补充信息。")]
    public void PetMotion(XmlExecutorContext context, string mtn)
    {
        if (context.CallMode != CallMode.OneShot)
            return;
        mtn = mtn.Trim();
        if (string.IsNullOrWhiteSpace(mtn))
            return;
        if (client.SupportedMotions.TryGetValue(mtn, out (string Group, int Index) motion) == false)
            return;

        client.PlayMotion(motion.Group, motion.Index);
    }
    [XmlFunction("move")]
    [Description("在屏幕上进行相对位置位移。示例: <pmove x=\"200\" y=\"100\" duration=\"500\" /> - 表示在0.5秒内从当前位置向右移200像素，下移100像素")]
    public Task PetMove(XmlExecutorContext context, double x = 0, double y = 0, int duration = 1000)
    {
        if (context.CallMode != CallMode.OneShot)
            return Task.CompletedTask;

        return client.MoveAsync(x, y, duration);
    }
    [XmlFunction("pos")]
    [Description("请求系统返回当前所在位置（使用后需要等待系统响应）。")]
    public async Task PetPos(XmlExecutorContext context)
    {
        if (context.CallMode != CallMode.OneShot)
            return;

        try
        {
            (double x, double y) = await client.GetPositionAsync();
            chatBot.Poke($"[DeskPetService] 当前位置: x={x}, y={y}");
        }
        catch (TimeoutException)
        {
            chatBot.Poke("[DeskPetService] 获取坐标超时");
        }
    }

    ChatBot chatBot = null!;
    PetServer client = null!;
    long lastBubbleEndTime;

    public DeskPetService(InterpreterService interpreterService)
    {
        interpreterService.RegisterHandler(this);
    }
    public async ValueTask DisposeAsync()
    {
        await client.DisposeAsync();
    }
    public override Task AwakeAsync(AwakeContext context)
    {
        client = new PetServer("Mao/Mao.model3.json");
        string supportedExpressionsDescription = string.Join(", ", client.SupportedExpressions);
        string supportedMotionsDescription = string.Join(", ", client.SupportedMotions.Keys);
        context.contextBuilder.ChatHistory.AddSystemMessage(
            $"""
             # DeskPetService 补充信息

             ## 表情动作
                - 支持的 exp（表情）：{supportedExpressionsDescription}
                - 支持的 mtn（动作）：{supportedMotionsDescription}
                - 注意：如果要发送表情动作，一定要在说话之前，因为说话时会阻塞线程！
                
             ## 位置移动
                - 当前屏幕分辨率：{AlifeDevice.GetResolution()}
                - 注意：移动是相对移动，如果要进行绝对移动，必须先确认自身位置！
             """);
        return Task.CompletedTask;
    }
    public override async Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        chatBot = chatActivity.ChatBot;

        await client.WaitReadyAsync();
        client.OnInput += text => chatBot.Chat("[DeskPetService] " + text);
        client.OnInteracted += text => chatBot.Poke("[DeskPetService] (交互: " + text + ")");
    }
}
