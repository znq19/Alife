using System.Text;
using Alife.Framework;
using Alife.Function.Interpreter;
using Microsoft.SemanticKernel;

namespace Alife.Implement;

[Plugin("函数调用", "为AI增加一种基于Xml的流式函数执行功能，实现快速实时的交互能力。", launchOrder: -1000)]
public class FunctionService : InteractivePlugin<FunctionService>
{
    public bool IsIdle => executor.IsIdle;

    public void RegisterHandler(XmlHandler handler, params string[] plainAreas)
    {
        handlerTable.Register(handler);
        this.plainAreas.AddRange(plainAreas);
    }

    public void UnregisterHandler(XmlHandler handler) => handlerTable.Unregister(handler);

    public void RegisterHandler(object handler, params string[] plainAreas)
    {
        handlerTable.Register(new XmlHandler(handler));
        this.plainAreas.AddRange(plainAreas);
    }

    public void UnregisterHandler(object handler) => handlerTable.Unregister(new XmlHandler(handler));

    readonly XmlHandlerTable handlerTable = new();
    readonly List<string> plainAreas = new();
    XmlStreamParser parser = null!;
    XmlStreamExecutor executor = null!;

    public override async Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        await base.StartAsync(kernel, chatActivity);

        //创建xml解析执行器等
        parser = new XmlStreamParser(plainAreas.ToArray());
        executor = new XmlStreamExecutor(
            parser,
            handlerTable,
            ["，", "。", "！", "？", "......", "~"],
            minBreakingLength: 9
        );
        parser.Error += OnError;
        executor.Error += OnError;

        //注入使用说明
        string prompt = $"""
                         你拥有输出特定的xml标签来实现功能调用的能力（虽然你也可以直接输出普通文本，但那样通常无法被外界看到或听到）。
                         注意！由于xml的解释器的存在，【" | & | < | >】之类的xml符号都无法直接输出，你需要使用xml转义的方式【&quot; | &amp; | &lt; | &gt;】来输出尖括号。如果你在调用其他标签功能时，出现了因Xml符号中断导致的异常，你可以尝试将其转义输出来解决。

                         ## 目前支持的标签和说明文档
                         {handlerTable.Document()}
                         """;

        chatActivity.ChatBot.ChatHistory.AddSystemMessage(prompt);
        chatActivity.ChatBot.ChatReceived += OnChatReceived;
        chatActivity.ChatBot.ChatSent += OnChatSent;
        chatActivity.ChatBot.ChatOver += OnChatOver;
    }

    public override async Task DestroyAsync()
    {
        await Task.Run(async () =>
        {
            while (executor.IsIdle == false)
                await Task.Yield();
        });
        await executor.DisposeAsync();

        await base.DestroyAsync();
    }

    void OnChatSent(string _)
    {
        executor.Reset();
    }

    void OnChatOver()
    {
        executor.Flush();
    }

    void OnChatReceived(string obj)
    {
        executor.Feed(obj);
    }

    void OnError(string tag, Exception exception)
    {
        Poke($"执行{tag}标签出错：{exception.Message}");
    }
}