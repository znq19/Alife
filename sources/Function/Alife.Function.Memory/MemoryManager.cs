namespace Alife.Function.Memory;


/// <summary>
/// 记忆核心管理器。协调存储、索引和压缩逻辑。
/// 实现层级化索引关系：每个摘要记录都描述了其涵盖的对话范围和时间跨度。
/// </summary>
public class MemoryManager
{
    public int CompressThreshold { get; set; } = 5;
    public int CompressBatchSize { get; set; } = 3;

    public MemoryManager(MemoryStorage storage, MemoryIndex index, MemoryCompressor compressor)
    {
        this.storage = storage;
        this.index = index;
        this.compressor = compressor;
    }

    /// <summary>
    /// 初始化并加载角色的活跃前沿记忆。
    /// </summary>
    public void Initialize(string characterId)
    {
        lock (activeLevels)
        {
            if (activeLevels.ContainsKey(characterId)) return;
            
            var loaded = storage.LoadFrontier(characterId);
            if (loaded != null)
            {
                activeLevels[characterId] = loaded;
                // 从所有非 Level 0 的摘要中载入向量索引以便召回
                foreach (var level in loaded.Keys.Where(k => k > 0))
                {
                    index.LoadExisting(loaded[level]);
                }
                
                // 恢复计数器
                lock (levelCounters)
                {
                    var counters = new Dictionary<int, int>();
                    foreach (var level in loaded.Keys)
                    {
                        if (loaded[level].Count > 0)
                            counters[level] = loaded[level].Max(r => r.RangeEnd);
                    }
                    levelCounters[characterId] = counters;
                }
            }
            else
            {
                activeLevels[characterId] = new Dictionary<int, List<MemoryRecord>>();
            }
        }
    }

    /// <summary>
    /// 将当前的活跃层级持久化到磁盘快照。
    /// </summary>
    public void SaveFrontier(string characterId)
    {
        lock (activeLevels)
        {
            if (activeLevels.TryGetValue(characterId, out var levels))
            {
                storage.SaveFrontier(characterId, levels);
            }
        }
    }

    /// <summary>
    /// 添加一条最原始的对话记录 (Level 0)。
    /// 返回是否触发了归档压缩。
    /// </summary>
    public async Task<bool> AddAsync(string characterId, string userMessage, string assistantMessage)
    {
        string content = $"用户：{userMessage}\n回复：{assistantMessage}";
        
        int nextId = GetNextCounter(characterId, 0);
        MemoryRecord record = new() {
            CharacterId = characterId,
            Level = 0,
            Content = content,
            RangeStart = nextId,
            RangeEnd = nextId,
            StartTime = DateTimeOffset.Now,
            EndTime = DateTimeOffset.Now
        };

        // 注意：这里不再单独 save 到磁盘，而是由定期快照或归档驱动持久化
        await index.IndexAsync(record); 
        
        lock (activeLevels)
        {
            if (!activeLevels[characterId].ContainsKey(0)) activeLevels[characterId][0] = new();
            activeLevels[characterId][0].Add(record);
        }

        return await CompressIfNeededAsync(characterId, 0);
    }

    /// <summary>
    /// 语义召回（仅限摘要层）。
    /// </summary>
    public Task<List<MemoryRecord>> RecallAsync(string characterId, string query, int topK = 5)
    {
        return index.SearchAsync(query, topK);
    }

    /// <summary>
    /// 溯源展开：读取一条摘要记录对应的原始归档批次。
    /// </summary>
    public async Task<List<MemoryRecord>> GetArchiveAsync(string characterId, string summaryId)
    {
        // 直接从存储层根据 ID 读取归档文件
        return await Task.Run(() => storage.LoadArchive(characterId, summaryId) ?? []);
    }

    /// <summary>
    /// 获取“记忆前沿”：即所有层级中尚未被进一步压缩的活跃记录。
    /// </summary>
    public IReadOnlyList<MemoryRecord> GetTopActiveMemories(string characterId)
    {
        List<MemoryRecord> frontier = new();
        lock (activeLevels)
        {
            if (!activeLevels.TryGetValue(characterId, out var levels))
                return frontier;

            // 从最高层到底层，收集所有活跃记忆
            var sortedLevels = levels.Keys.OrderByDescending(k => k);
            foreach (var level in sortedLevels)
            {
                frontier.AddRange(levels[level]);
            }
        }
        return frontier;
    }

    readonly MemoryStorage storage;
    readonly MemoryIndex index;
    readonly MemoryCompressor compressor;
    
    // CharacterId -> Level -> Records
    readonly Dictionary<string, Dictionary<int, List<MemoryRecord>>> activeLevels = new();
    // CharacterId -> Level -> LastCounter
    readonly Dictionary<string, Dictionary<int, int>> levelCounters = new();

    int GetNextCounter(string characterId, int level)
    {
        lock (levelCounters)
        {
            if (!levelCounters.TryGetValue(characterId, out var levels))
            {
                levels = new Dictionary<int, int>();
                levelCounters[characterId] = levels;
            }

            if (!levels.TryGetValue(level, out int counter))
                counter = 0;

            levels[level] = counter + 1;
            return levels[level];
        }
    }

    async Task<bool> CompressIfNeededAsync(string characterId, int level)
    {
        List<MemoryRecord> levelRecords;
        lock (activeLevels)
        {
            if (!activeLevels[characterId].TryGetValue(level, out levelRecords!) || levelRecords.Count < CompressThreshold)
                return false;
        }

        // 提取待压缩的一组
        List<MemoryRecord> batch;
        lock (activeLevels)
        {
            batch = levelRecords.Take(CompressBatchSize).ToList();
            levelRecords.RemoveRange(0, CompressBatchSize);
        }

        // AI 生成摘要
        MemoryRecord summary = await compressor.CompressAsync(batch, characterId);
        
        // 关键点：将产生该摘要的原始批次“归档”到磁盘，实现溯源链路
        storage.SaveArchive(characterId, summary.Id, batch);
        
        // 摘要本身作为索引，载入内存索引
        await index.IndexAsync(summary);

        lock (activeLevels)
        {
            if (!activeLevels[characterId].ContainsKey(level + 1)) activeLevels[characterId][level + 1] = new();
            activeLevels[characterId][level + 1].Add(summary);
        }

        // 递归检查
        await CompressIfNeededAsync(characterId, level);
        await CompressIfNeededAsync(characterId, level + 1);
        return true;
    }

}

