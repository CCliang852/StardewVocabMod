namespace StardewVocabMod.Data;

/// <summary>
/// 单个单词条目
/// </summary>
public class WordEntry
{
    /// <summary>英文单词</summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>中文释义</summary>
    public string Chinese { get; set; } = string.Empty;

    /// <summary>难度标签（如 cet6, ielts, custom）</summary>
    public string Difficulty { get; set; } = "cet6";

    /// <summary>词根前缀（如 con-, pre-, re-），用于挑战模式</summary>
    public string RootPrefix { get; set; } = string.Empty;
}
