using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alife.Framework;
using Alife.Function.Interpreter;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Alife.Function.FunctionCaller;

[Module("Xml函数执行器", "提供一种Xml函数调用框架，可以将注册其中的函数，暴露给AI，并指导其用Xml标签调用。",
    defaultCategory: "Alife 官方/功能底座",
    launchOrder: -1000)]
public class XmlFunctionCaller(ILogger<XmlFunctionCaller> logger) : InteractiveModule<XmlFunctionCaller>
{
    public bool IsIdle => executor.IsInactive;

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
            ["，", "。", "！", "？", "......", "~", "…"],
            minBreakingLength: 9
        );
        parser.Error += OnError;
        executor.Error += OnError;

        chatActivity.ChatBot.ChatReceived += OnChatReceived;
        chatActivity.ChatBot.ChatSent += OnChatSent;

        Prompt($"""
                默认情况下你仅支持输出普通文本，但由于各种插件服务的存在，使得你还拥有通过输出特定的xml标签执行功能调用的能力。

                ## 可用函数
                如下函数收集于各种服务插件中，你需要配合描述以及相关服务本身的文档，按规则去使用他们。

                {handlerTable.Document()}

                ## 注意事项
                1. 由于xml的解释器的存在，【" | & | < | >】之类的xml符号都无法直接输出，你需要使用xml转义的方式【&quot; | &amp; | &lt; | &gt;】来输出尖括号。
                2. 仅支持上述提到的函数可以使用，其他任何xml标签都不支持。

                ## 使用示例
                当你的函数足够丰富后，你可以尝试用如下的方式使用他们，这是官方最佳示例：
                ```
                (可选，未被标签包裹的文字，用户看不到，所以可以在此实现空消息、自言自语、思考等动作)
                <say> <!-- 默认采用say进行对外输出，并在文本中穿插表情动作，来实现动态的交互效果 -->
                主人你看我画的好不好看，<exp opt="开心" />今天特意给你画的噢！<motion opt="不好意思"/>
                看你每天那么累，给你打打气。
                </say>
                <python> <!-- 因为python执行需要时间，在结尾调用比较合适。 -->
                show('cheer.png')
                <python>
                ```   
                """);
    }

    public override async Task DestroyAsync()
    {
        await executor.WaitToInactive();
        await executor.DisposeAsync();

        await base.DestroyAsync();
    }

    async void OnChatSent(string _)
    {
        try
        {
            await ChatBot.RequestChatAsync();
            try
            {
                executor.Flush();
                await executor.WaitToInactive(ChatBot.ChatBreakTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                await executor.CancelAsync();
            }
            finally
            {
                ChatBot.ReleaseChat();
            }
        }
        catch (OperationCanceledException) {}
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    void OnChatReceived(string obj)
    {
        executor.Feed(obj);
    }

    void OnError(string tag, Exception exception)
    {
        Poke($"执行{tag}标签出错：{exception.Message}");
        logger.LogWarning(exception, $"执行{tag}标签出错");
    }
}
