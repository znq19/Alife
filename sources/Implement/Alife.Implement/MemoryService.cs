using System.ComponentModel;
using Alife.Basic;
using Alife.Framework;
using Alife.Function.Memory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Alife.Function.Interpreter;


namespace Alife.Implement;

public class MemoryServiceConfig
{
    public int CompressThreshold { get; set; } = 5;
    public int CompressBatchSize { get; set; } = 2;
}


[Plugin("记忆储存(层级化)", "基于虚拟页表思想的层级化归档压缩记忆系统，实现逻辑上的无限上下文。", launchOrder: 100)]
public class MemoryService : Plugin, IConfigurable<MemoryServiceConfig>
{
    readonly StorageSystem storageSystem;
    MemoryManager? memoryManager;
    MemoryServiceConfig configuration = new();
    ChatBot? chatBot;
    string? characterId;
    string? lastUserMessage;
    string? lastAssistantMessage;
    string? staticMemoryPath;


    public MemoryService(StorageSystem storageSystem, InterpreterService interpreterService)
    {
        this.storageSystem = storageSystem;
        interpreterService.RegisterHandler(this);
    }


    public void Configure(MemoryServiceConfig configuration)
    {
        this.configuration = configuration;
        if (memoryManager != null)
        {
            memoryManager.CompressThreshold = configuration.CompressThreshold;
            memoryManager.CompressBatchSize = configuration.CompressBatchSize;
        }
    }


    public override async Task AwakeAsync(AwakeContext context)
    {
        characterId = context.character.ID;
        // 获取 SK 服务
        var kernel = context.kernelBuilder.Build();
        var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        // 初始化内存框架
        var storage = new MemoryStorage(storageSystem.GetStoragePath());
        var index = new MemoryIndex(embeddingService, "all-minilm-l6-v2");
        var compressor = new MemoryCompressor(chatService);
        
        memoryManager = new MemoryManager(storage, index, compressor);
        memoryManager.CompressThreshold = configuration.CompressThreshold;
        memoryManager.CompressBatchSize = configuration.CompressBatchSize;

        // 设置静态记忆路径并初始化
        staticMemoryPath = Path.Combine(storageSystem.GetStoragePath(), "Memory", characterId, "静态记忆.md");
        EnsureStaticMemoryInitialized();

        // 1. 从磁盘加载该角色的分层记忆前沿 (Frontier)
        memoryManager.Initialize(characterId);

        // 2. 将加载的记忆前沿同步到活跃 ChatHistory
        RefreshChatHistory(context.contextBuilder.ChatHistory, characterId);

        // 注入静态内容为 System 消息
        InjectInitialInstructions(context.contextBuilder.ChatHistory);
        
        await Task.CompletedTask;
    }

    private void InjectInitialInstructions(ChatHistory chatHistory)
    {
        if (chatHistory.Any(m => m.Role == AuthorRole.System && m.Content != null && m.Content.Contains("Memory System")))
            return;

        string instruction = @"# 记忆与索引操作指南 (Memory System)

你可以通过以下 XML 标签来操作你的层级化记忆系统：
1. <recall query=""关键词"" /> : 根据语义搜索历史摘要。结果通过系统消息反馈。
2. <expand id=""记录ID"" /> : 展开某条摘要下的详细原始对话。
3. <memo>新内容</memo> : 更新你的核心备忘录。修改过程会被记录，作为认知的巩固。

请合理使用这些标签来维护你的长期认知。";

        chatHistory.AddSystemMessage(instruction);
    }

    private void EnsureStaticMemoryInitialized()
    {
        if (string.IsNullOrEmpty(staticMemoryPath)) return;
        string? dir = Path.GetDirectoryName(staticMemoryPath);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        if (!File.Exists(staticMemoryPath))
        {
            // 尝试迁移旧数据
            string coreMemoryPath = Path.Combine(dir!, "核心记忆.md");
            string memoPath = Path.Combine(dir!, "备忘录.md");
            System.Text.StringBuilder sb = new();
            if (File.Exists(coreMemoryPath))
            {
                sb.AppendLine("## 核心记忆 (迁移)");
                sb.AppendLine(File.ReadAllText(coreMemoryPath));
                File.Delete(coreMemoryPath);
            }
            if (File.Exists(memoPath))
            {
                sb.AppendLine("\n## 备忘录 (迁移)");
                sb.AppendLine(File.ReadAllText(memoPath));
                File.Delete(memoPath);
            }

            File.WriteAllText(staticMemoryPath, sb.Length > 0 ? sb.ToString() : "# 静态记忆\n\n在此记下需要永久记住的重要信息。");
        }
    }

    private void RefreshStaticMemory()
    {
        if (string.IsNullOrEmpty(staticMemoryPath) || !File.Exists(staticMemoryPath) || chatBot == null) return;

        string staticContent = File.ReadAllText(staticMemoryPath).Trim();
        
        if (string.IsNullOrWhiteSpace(staticContent) || 
            staticContent == "# 静态记忆" || 
            staticContent.EndsWith("在此记下需要永久记住的重要信息。")) 
            return;

        chatBot.Poke($"[MemoryService] 核心静态记忆备份：\n---\n{staticContent}\n---");
    }

    public override Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        chatBot = chatActivity.ChatBot;
        
        chatBot.ChatSent += (msg) => lastUserMessage = msg;
        chatBot.ChatReceived += (msg) => 
        {
            lastAssistantMessage = (lastAssistantMessage ?? "") + msg;
        };
        chatBot.ChatOver += OnChatOver;

        RefreshStaticMemory();
        return Task.CompletedTask;
    }

    private async void OnChatOver()
    {
        if (memoryManager == null || chatBot == null || characterId == null || string.IsNullOrWhiteSpace(lastUserMessage) || string.IsNullOrWhiteSpace(lastAssistantMessage))
            return;

        try
        {
            // 1. 将新对话记入内存管理器
            bool archivingTriggered = await memoryManager.AddAsync(characterId, lastUserMessage, lastAssistantMessage);
            
            // 2. 同步到活跃 ChatHistory
            await chatBot.ChatSemaphore.WaitAsync();
            try
            {
                RefreshChatHistory(chatBot.ChatHistory, characterId);
            }
            finally
            {
                chatBot.ChatSemaphore.Release();
            }

            // 3. 持久化前沿上下文 (Frontier Persistence)
            memoryManager.SaveFrontier(characterId);

            // 4. 如果本轮触发了压缩归档，则刷新一遍静态记忆
            if (archivingTriggered)
            {
                // RefreshStaticMemory();
            }

            lastUserMessage = null;
            lastAssistantMessage = null;
        }
        catch (Exception e)
        {
            Terminal.LogWarning($"Memory archive failed: {e.Message}");
        }
    }

    private void RefreshChatHistory(ChatHistory history, string characterId)
    {
        var frontier = memoryManager!.GetTopActiveMemories(characterId);
            
        int historyIndex = 0;
        while (historyIndex < history.Count && history[historyIndex].Role == AuthorRole.System)
            historyIndex++;

        int frontierIndex = 0;
        // 差异化比对：寻找第一个不同点
        while (historyIndex < history.Count && frontierIndex < frontier.Count)
        {
            var record = frontier[frontierIndex];
            if (record.Level > 0)
            {
                // 摘要匹配
                if (history[historyIndex].Content == $"[历史回顾] {record.Content}")
                {
                    historyIndex++;
                    frontierIndex++;
                    continue;
                }
            }
            else
            {
                // Level 0 匹配：历史轴通常是 User+Assistant 两条，记录是合并的一条
                if (historyIndex + 1 < history.Count)
                {
                    string merged = $"用户：{history[historyIndex].Content}\n回复：{history[historyIndex + 1].Content}";
                    if (merged == record.Content)
                    {
                        historyIndex += 2;
                        frontierIndex++;
                        continue;
                    }
                }
            }
            break;
        }

        // 差异化更新
        if (historyIndex < history.Count || frontierIndex < frontier.Count)
        {
            while (history.Count > historyIndex)
                history.RemoveAt(historyIndex);

            for (; frontierIndex < frontier.Count; frontierIndex++)
            {
                var record = frontier[frontierIndex];
                if (record.Level > 0)
                {
                    history.AddAssistantMessage($"[历史回顾] {record.Content}");
                }
                else
                {
                    // 还原 L0 的角色结构
                    var parts = record.Content.Split("\n回复：", 2);
                    string userPart = parts[0].Replace("用户：", "");
                    string assistantPart = parts.Length > 1 ? parts[1] : "";
                    history.AddUserMessage(userPart);
                    history.AddAssistantMessage(assistantPart);
                }
            }

            if (chatBot != null)
            {
                var field = typeof(ChatBot).GetField("lastContentIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(chatBot, history.Count);
            }
        }
    }


    [XmlFunction("recall")]
    [Description("根据语义搜索你的历史摘要记录。建议输入具体的关键词语。用法: <recall query=\"关键词\" />")]
    public async Task Recall(XmlExecutorContext context, string query)
    {
        if (context.CallMode != CallMode.OneShot) return;
        if (memoryManager == null || characterId == null) return;
        
        var results = await memoryManager.RecallAsync(characterId, query);
        if (results.Count == 0)
        {
            chatBot?.Poke("[MemorySystem] 未找到相关的记忆摘要。");
            return;
        }

        System.Text.StringBuilder sb = new();
        sb.AppendLine($"[MemorySystem] 召回了以下相关摘要：");
        foreach (var r in results)
            sb.AppendLine($"- ID: {r.Id} (范围: {r.RangeStart}-{r.RangeEnd}) : {r.Content}");
        
        chatBot?.Poke(sb.ToString());
    }

    [XmlFunction("expand")]
    [Description("深入查看某条摘要下包含的详细原始记录。用法: <expand id=\"记录ID\" />")]
    public async Task Expand(XmlExecutorContext context, string id)
    {
        if (context.CallMode != CallMode.OneShot) return;
        if (memoryManager == null || characterId == null) return;
        
        var details = await memoryManager.GetArchiveAsync(characterId, id);
        if (details.Count == 0)
        {
            chatBot?.Poke("[MemorySystem] 未找到该摘要对应的归档明细，或者该 ID 无效。");
            return;
        }

        System.Text.StringBuilder sb = new();
        sb.AppendLine($"[MemorySystem] 摘要 {id} 的归档明细记录：");
        sb.AppendLine("---");
        foreach (var record in details)
            sb.AppendLine($"[{record.StartTime:HH:mm}] {record.Content}\n");
        sb.AppendLine("---");
        
        chatBot?.Poke(sb.ToString());
    }

    [XmlFunction("memo")]
    [Description("更新你的核心备忘录内容。这是一次性覆盖，请整理好你要永久记住的信息。用法: <memo>内容</memo>")]
    public async Task Memo(XmlExecutorContext context, [XmlContent] string content)
    {
        if (context.CallMode != CallMode.Closing) return;
        if (string.IsNullOrEmpty(staticMemoryPath)) return;

        await File.WriteAllTextAsync(staticMemoryPath, context.FullContent);
        chatBot?.Poke("[MemorySystem] 静态记忆已成功更新并持久化。内容备份如下：\n" + context.FullContent);
    }



}
