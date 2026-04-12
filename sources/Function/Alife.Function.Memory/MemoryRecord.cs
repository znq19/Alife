namespace Alife.Function.Memory;

/// <summary>
/// 一条完整记忆的数据容器。
/// Level 0 = 原始对话，Level N+1 = 对 N 层若干条记录的摘要。
/// </summary>
public class MemoryRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string CharacterId { get; init; } = null!;
    public int Level { get; init; }
    public string Content { get; init; } = null!;
    public string[] ChildIds { get; init; } = [];
    
    // 对话范围索引 (相对于上一层级)
    public int RangeStart { get; init; }
    public int RangeEnd { get; init; }
    
    // 时间跨度
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }

    public float[] Embedding { get; set; } = [];
    public string? EmbeddingModel { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
