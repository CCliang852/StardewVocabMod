using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewModdingAPI;

namespace StardewVocabMod.Data;

/// <summary>
/// 词库管理类：加载、查询、自定义导入
/// </summary>
public class WordBank
{
    private readonly IMonitor _monitor;
    private List<WordEntry> _words = new();
    private Dictionary<string, WordRelation> _relations = new();
    private readonly Random _random = new();

    /// <summary>词库中的单词总数</summary>
    public int WordCount => _words.Count;

    /// <summary>关系表中的单词数</summary>
    public int RelationCount => _relations.Count;

    public WordBank(IMonitor monitor)
    {
        _monitor = monitor;
    }

    /// <summary>
    /// 加载内置词库 JSON 文件
    /// </summary>
    public void LoadBuiltInWords(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _monitor.Log($"内置词库文件未找到: {filePath}", LogLevel.Warn);
                return;
            }

            var json = File.ReadAllText(filePath);
            var words = System.Text.Json.JsonSerializer.Deserialize<List<WordEntry>>(json);

            if (words != null && words.Count > 0)
            {
                _words.AddRange(words);
                _monitor.Log($"内置词库已加载: {words.Count} 个单词", LogLevel.Info);
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"内置词库加载失败: {ex.Message}", LogLevel.Error);
        }
    }

    /// <summary>
    /// 加载词关系表 JSON 文件
    /// </summary>
    public void LoadRelations(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _monitor.Log($"词关系表文件未找到: {filePath}", LogLevel.Warn);
                return;
            }

            var json = File.ReadAllText(filePath);
            var relations = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, WordRelation>>(json);

            if (relations != null)
            {
                _relations = relations;
                _monitor.Log($"词关系表已加载: {relations.Count} 条", LogLevel.Info);
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"词关系表加载失败: {ex.Message}", LogLevel.Error);
        }
    }

    /// <summary>
    /// 导入玩家自定义词库（.txt 或 .json）
    /// </summary>
    /// <param name="filePath">词库文件路径</param>
    /// <returns>成功导入的词数，-1 表示失败</returns>
    public int ImportCustomWords(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _monitor.Log($"自定义词库文件未找到: {filePath}", LogLevel.Warn);
                return -1;
            }

            var ext = Path.GetExtension(filePath).ToLower();
            List<WordEntry>? imported = null;

            switch (ext)
            {
                case ".json":
                    imported = ImportFromJson(filePath);
                    break;
                case ".txt":
                    imported = ImportFromTxt(filePath);
                    break;
                default:
                    _monitor.Log($"不支持的文件格式: {ext}，请使用 .json 或 .txt", LogLevel.Warn);
                    return -1;
            }

            if (imported != null && imported.Count > 0)
            {
                _words.AddRange(imported);
                _monitor.Log($"自定义词库已导入: {imported.Count} 个单词", LogLevel.Info);
                return imported.Count;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _monitor.Log($"自定义词库导入失败: {ex.Message}", LogLevel.Error);
            return -1;
        }
    }

    /// <summary>
    /// 从 JSON 文件导入词库
    /// </summary>
    private List<WordEntry>? ImportFromJson(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return System.Text.Json.JsonSerializer.Deserialize<List<WordEntry>>(json);
    }

    /// <summary>
    /// 从 TXT 文件导入词库（格式：每行 "英文 中文释义"，空格/Tab 分隔）
    /// </summary>
    private List<WordEntry>? ImportFromTxt(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var words = new List<WordEntry>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                continue;

            // 尝试空格或 Tab 分隔
            var parts = trimmed.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                words.Add(new WordEntry
                {
                    Word = parts[0].Trim(),
                    Chinese = parts[1].Trim(),
                    Difficulty = "custom"
                });
            }
            else if (parts.Length == 1 && !string.IsNullOrEmpty(parts[0]))
            {
                // 只有英文单词，没有释义
                words.Add(new WordEntry
                {
                    Word = parts[0].Trim(),
                    Chinese = "(待补充)",
                    Difficulty = "custom"
                });
            }
        }

        return words;
    }

    /// <summary>
    /// 随机获取指定数量的单词
    /// </summary>
    public List<WordEntry> GetRandomWords(int count)
    {
        if (_words.Count == 0) return new List<WordEntry>();

        count = Math.Min(count, _words.Count);
        return _words.OrderBy(_ => _random.Next()).Take(count).ToList();
    }

    /// <summary>
    /// 根据英文单词查找词条
    /// </summary>
    public WordEntry? FindWord(string word)
    {
        return _words.FirstOrDefault(w =>
            string.Equals(w.Word, word, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取指定单词的关系（近义词/形近词）
    /// </summary>
    public WordRelation? GetRelation(string word)
    {
        var key = word.ToLower();
        return _relations.GetValueOrDefault(key);
    }

    /// <summary>
    /// 获取指定单词的近义词/形近词对应的词条列表（用于出题）
    /// </summary>
    public List<WordEntry> GetRelatedWords(string word)
    {
        var result = new List<WordEntry>();
        var relation = GetRelation(word);
        if (relation == null) return result;

        // 收集所有关联词
        var relatedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        relatedWords.UnionWith(relation.Synonyms);
        relatedWords.UnionWith(relation.Lookalikes);
        relatedWords.UnionWith(relation.SameRoot);

        // 在词库中查找对应词条
        foreach (var related in relatedWords)
        {
            var entry = FindWord(related);
            if (entry != null)
                result.Add(entry);
        }

        return result;
    }

    /// <summary>
    /// 获取所有有相同词根前缀的单词（用于挑战模式）
    /// </summary>
    public List<WordEntry> GetWordsByRootPrefix(string prefix, int minCount = 10)
    {
        var result = _words
            .Where(w => w.Word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return result.Count >= minCount ? result : new List<WordEntry>();
    }

    /// <summary>
    /// 获取可用的词根前缀列表（至少有 minCount 个单词的词根才能用于挑战）
    /// </summary>
    public List<string> GetAvailablePrefixes(int minCount = 15)
    {
        var prefixCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var word in _words)
        {
            if (word.Word.Length < 4) continue;
            var prefix = word.Word[..3].ToLower(); // 取前 3 个字母作为前缀
            if (prefixCounts.ContainsKey(prefix))
                prefixCounts[prefix]++;
            else
                prefixCounts[prefix] = 1;
        }

        return prefixCounts
            .Where(kv => kv.Value >= minCount)
            .Select(kv => kv.Key)
            .OrderBy(_ => _random.Next())
            .ToList();
    }

    /// <summary>
    /// 为指定单词生成 3 个中文选项（1 正确 + 2 干扰项）
    /// </summary>
    public List<string> GenerateOptions(string word, int optionCount = 3)
    {
        var correct = FindWord(word);
        if (correct == null) return new List<string>();

        var options = new List<string> { correct.Chinese };

        // 从其他单词中随机选中文释义作为干扰项
        var otherWords = _words
            .Where(w => !string.Equals(w.Word, word, StringComparison.OrdinalIgnoreCase))
            .OrderBy(_ => _random.Next())
            .Take(optionCount - 1);

        foreach (var other in otherWords)
        {
            if (!options.Contains(other.Chinese))
                options.Add(other.Chinese);
        }

        // 打乱顺序
        return options.OrderBy(_ => _random.Next()).ToList();
    }
}
