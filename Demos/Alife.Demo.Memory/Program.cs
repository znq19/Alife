using Alife.Basic;
using Alife.Framework;
using Alife.Implement;
using System;
using System.Text;
using Microsoft.SemanticKernel.ChatCompletion;

Terminal.Log("========================================", ConsoleColor.Cyan);
Terminal.Log("   分层记忆系统 (Memory System) 验证 Demo", ConsoleColor.Cyan);
Terminal.Log("   配置：阈值 3 条，归档 2 条", ConsoleColor.Cyan);

Terminal.Log("========================================", ConsoleColor.Cyan);

// 1. 配置演示内容
var character = new Character {
    ID = "MemoryDemoBot",
    Name = "记忆助手",
    Prompt = "你是一个拥有长期记忆能力的助手。请尽量简洁地回答用户。",
    Plugins = new HashSet<Type> {
        typeof(InterpreterService),
        typeof(LocalEmbeddingService),
        typeof(MemoryService),
        typeof(OpenAIChatService),
    }
};

// 2. 初始化标准套件
var suite = await DemoSuite.InitializeAsync(character);

// 3. 配置 MemoryService 演示参数
// 注意：DemoSuite 会初始化 ConfigSystem，我们可以通过 suite.Configuration 访问
var memoryConfig = new MemoryServiceConfig {
    CompressThreshold = 5,
    CompressBatchSize = 2
};
suite.Configuration.SetConfiguration(typeof(MemoryService), memoryConfig);

// 4. 注入历史轴实时探测器 (探测分层记忆的变化)
suite.ChatBot.ChatOver += () => {
    PrintHistoryStructure(suite.ChatBot.ChatHistory);
};

Terminal.LogInfo("提示：你可以连续输入多条短消息，观察 Index 0-1 的稳定性，以及 L1 摘要的生成。");
Terminal.LogInfo("输入 'exit' 退出。");
Terminal.Log("--------------------------------------------------\n", ConsoleColor.Gray);

// 5. 运行交互循环
await suite.RunAsync();

Terminal.Log("演示结束。", ConsoleColor.Cyan);




void PrintHistoryStructure(ChatHistory history)
{
    lock (Terminal.ConsoleLock)
    {
        Console.WriteLine("\n[探测器] 当前上下文物理结构监控:");
        Console.WriteLine("----------------------------------------------------------------------");
        for (int i = 0; i < history.Count; i++)
        {
            var msg = history[i];
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"{i:00} | ");

            if (msg.Role == AuthorRole.System)
            {
                if (msg.Content?.Contains("Memory System") == true)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("[INSTRUCTION] ");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write("[SYSTEM     ] ");
                }
            }
            else if (msg.Content != null && msg.Content.Contains("[历史回顾"))
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("[SUMMARY L1+] ");
            }
            else if (msg.Content != null && msg.Content.Contains("核心静态记忆备份"))
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write("[POKE REFRESH] ");
            }
            else
            {
                Console.ForegroundColor = msg.Role == AuthorRole.User ? ConsoleColor.White : ConsoleColor.Green;
                Console.Write($"[{msg.Role.ToString().ToUpper(),-12}] ");
            }

            string preview = msg.Content?.Length > 60 ? msg.Content.Substring(0, 57).Replace("\n", " ") + "..." : msg.Content?.Replace("\n", " ") ?? "";
            Console.WriteLine(preview);
        }
        Console.WriteLine("----------------------------------------------------------------------\n");
        Console.ResetColor();
    }
}
