using System.Collections.Generic;

namespace StardewVocabMod.Data;

/// <summary>
/// 单词的近义词/形近词关系
/// </summary>
public class WordRelation
{
    /// <summary>近义词/同义词列表</summary>
    public List<string> Synonyms { get; set; } = new();

    /// <summary>形近词列表</summary>
    public List<string> Lookalikes { get; set; } = new();

    /// <summary>同词根词列表</summary>
    public List<string> SameRoot { get; set; } = new();
}
