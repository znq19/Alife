namespace Alife.Function.Memory;

/// <summary>
/// 向量语义索引。仅保留摘要层 (L1+) 在内存中，实现大规模数据的低 RAM 占用。
/// L0 原始记录不进入全局向量池，仅在需要时按需搜索。
/// </summary>
public class MemoryIndex
{
    public MemoryIndex(TextVectorizer vectorizer, string modelName = "all-minilm-l6-v2")
    {
        this.vectorizer = vectorizer;
        this.modelName = modelName;
    }

    /// <summary>
    /// 索引一条记忆。
    /// 即使不进入全局索引（Level 0），也会确保存储了向量和模型名称。
    /// </summary>
    public async Task IndexAsync(MemoryRecord record)
    {
        if (record.Embedding.Length == 0 || record.EmbeddingModel != modelName)
        {
            record.Embedding = await vectorizer.VectorizeAsync(record.Content);
            record.EmbeddingModel = modelName;
        }

        // 仅将摘要层 (L1+) 载入常驻内存索引
        if (record.Level > 0)
        {
            lock (index)
                index[record.Id] = record;
        }
    }

    /// <summary>
    /// 加载已有记忆到内存。仅加载 Level > 0 的摘要。
    /// </summary>
    public void LoadExisting(IEnumerable<MemoryRecord> records)
    {
        lock (index)
        {
            foreach (var record in records)
            {
                if (record.Level > 0 && record.Embedding.Length > 0 && record.EmbeddingModel == modelName)
                    index[record.Id] = record;
            }
        }
    }

    /// <summary>
    /// 语义搜索（仅限内存中的摘要层）。
    /// </summary>
    public async Task<List<MemoryRecord>> SearchAsync(string query, int topK = 5)
    {
        float[] queryEmbedding = await vectorizer.VectorizeAsync(query);

        List<(float Score, MemoryRecord Record)> scored;
        lock (index)
        {
            scored = index.Values
                .Select(r => (CosineSimilarity(queryEmbedding, r.Embedding), r))
                .OrderByDescending(x => x.Item1)
                .Take(topK)
                .ToList();
        }

        return scored.Select(x => x.Record).ToList();
    }

    public void Remove(string recordId)
    {
        lock (index)
            index.Remove(recordId);
    }

    readonly TextVectorizer vectorizer;
    readonly string modelName;
    readonly Dictionary<string, MemoryRecord> index = new();

    static float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return normA == 0 || normB == 0 ? 0 : dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }
}
