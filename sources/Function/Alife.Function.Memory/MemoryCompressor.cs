using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Alife.Function.Memory;

/// <summary>
/// AI 压缩器。根据内容生成高层摘要。
/// </summary>
public class MemoryCompressor
{
    public MemoryCompressor(IChatCompletionService chatService)
    {
        this.chatService = chatService;
    }

    /// <summary>
    /// 将同层级的若干条记忆总结为一条上层摘要记忆。
    /// </summary>
    public async Task<MemoryRecord> CompressAsync(IReadOnlyList<MemoryRecord> records, string characterId)
    {
        string combinedContent = BuildCombinedContent(records);

        ChatHistory chatHistory = new();
        chatHistory.AddUserMessage(
            $"""
             请将以下 {records.Count} 条对话记录提炼为一个简短的摘要。
             你需要从上帝视角概括这段对话发生了什么。
             只需输出摘要本身，不要有任何多余的解释。

             {combinedContent}
             """);

        ChatMessageContent result = await chatService.GetChatMessageContentAsync(chatHistory);
        string summaryText = result.Content ?? combinedContent;

        // 计算时间跨度
        var startTime = records.Min(r => r.StartTime);
        if (startTime == default) startTime = records.Min(r => r.CreatedAt);
        
        var endTime = records.Max(r => r.EndTime);
        if (endTime == default) endTime = records.Max(r => r.CreatedAt);

        int level = records[0].Level + 1;
        int rangeStart = records.Min(r => r.RangeStart);
        int rangeEnd = records.Max(r => r.RangeEnd);

        // 拼接自描述报文头
        string finalContent = $"[历史回顾 (L{level}) #{rangeStart}-{rangeEnd}] [{startTime:yyyy/MM/dd HH:mm} - {endTime:HH:mm}]\n{summaryText}";

        return new MemoryRecord {
            CharacterId = characterId,
            Level = level,
            Content = finalContent,
            ChildIds = records.Select(r => r.Id).ToArray(),
            RangeStart = rangeStart,
            RangeEnd = rangeEnd,
            StartTime = startTime,
            EndTime = endTime
        };
    }

    static string BuildCombinedContent(IReadOnlyList<MemoryRecord> records)
    {
        System.Text.StringBuilder sb = new();
        foreach (var r in records)
        {
            sb.AppendLine($"[时间: {r.CreatedAt:yyyy-MM-dd HH:mm}]");
            sb.AppendLine(r.Content);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    readonly IChatCompletionService chatService;
}
