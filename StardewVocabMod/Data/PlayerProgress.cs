using System.Collections.Generic;
using System.Linq;

namespace StardewVocabMod.Data;

/// <summary>
/// 玩家背词进度数据，随 SMAPI 存档保存
/// </summary>
public class PlayerProgress
{
    /// <summary>
    /// 错词记录：单词（小写） → 错误次数
    /// </summary>
    public Dictionary<string, int> WrongWordCounts { get; set; } = new();

    /// <summary>
    /// 正确记录：单词（小写） → 正确次数
    /// </summary>
    public Dictionary<string, int> CorrectWordCounts { get; set; } = new();

    /// <summary>
    /// 今日每个 NPC 已触发背词次数：NPC 名字 → 次数
    /// </summary>
    public Dictionary<string, int> NpcDailyCounts { get; set; } = new();

    /// <summary>
    /// 今日是否已触发过第一次闲聊（用于跳过首次对话）
    /// </summary>
    public HashSet<string> NpcFirstChatToday { get; set; } = new();

    // ===== 挑战模式相关 =====

    /// <summary>上次挑战完成/失败的日期（游戏日期）</summary>
    public int LastChallengeDay { get; set; } = -999;

    /// <summary>是否有进行中的挑战</summary>
    public bool HasActiveChallenge { get; set; } = false;

    /// <summary>挑战截止日期（游戏总天数）</summary>
    public int ChallengeDeadlineDay { get; set; }

    /// <summary>当前挑战已连对题数</summary>
    public int ChallengeStreak { get; set; }

    /// <summary>当前挑战的词根前缀</summary>
    public string ChallengePrefix { get; set; } = string.Empty;

    /// <summary>
    /// 记录答错一个单词
    /// </summary>
    public void RecordWrong(string word)
    {
        var key = word.ToLower().Trim();
        if (WrongWordCounts.ContainsKey(key))
            WrongWordCounts[key]++;
        else
            WrongWordCounts[key] = 1;
    }

    /// <summary>
    /// 记录答对一个单词
    /// </summary>
    public void RecordCorrect(string word)
    {
        var key = word.ToLower().Trim();
        if (CorrectWordCounts.ContainsKey(key))
            CorrectWordCounts[key]++;
        else
            CorrectWordCounts[key] = 1;

        // 如果连续答对 3 次以上，从错词表中移除（已掌握）
        if (CorrectWordCounts[key] >= 3 && WrongWordCounts.ContainsKey(key))
        {
            WrongWordCounts.Remove(key);
        }
    }

    /// <summary>
    /// 获取已掌握的单词数（连续答对 3 次以上）
    /// </summary>
    public int MasteredCount => CorrectWordCounts.Count(kv => kv.Value >= 3);

    /// <summary>
    /// 获取曾遇到过的单词总数（答对或答错过至少一次）
    /// </summary>
    public int WordsEncountered
    {
        get
        {
            var all = new HashSet<string>(WrongWordCounts.Keys, System.StringComparer.OrdinalIgnoreCase);
            foreach (var key in CorrectWordCounts.Keys)
                all.Add(key);
            return all.Count;
        }
    }

    /// <summary>
    /// 获取进度统计
    /// </summary>
    /// <param name="totalInBank">词库总词数</param>
    public ProgressStats GetStats(int totalInBank)
    {
        return new ProgressStats
        {
            TotalInBank = totalInBank,
            TotalWrong = WrongWordCounts.Count,
            TotalMastered = MasteredCount,
            TotalEncountered = WordsEncountered,
            IsAllMastered = WrongWordCounts.Count == 0 && MasteredCount >= totalInBank
        };
    }

    /// <summary>
    /// 获取错词列表，按错误次数降序排列
    /// </summary>
    public List<string> GetWrongWords(int maxCount = 50)
    {
        return WrongWordCounts
            .OrderByDescending(kv => kv.Value)
            .Take(maxCount)
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>
    /// 获取错词总数
    /// </summary>
    public int WrongWordTotal => WrongWordCounts.Count;

    /// <summary>
    /// 检查今日是否可与该 NPC 触发背词
    /// </summary>
    /// <returns>true 表示可以触发</returns>
    public bool CanTriggerNpcQuiz(string npcName, int dailyLimit)
    {
        if (!NpcDailyCounts.ContainsKey(npcName))
            return true;
        return NpcDailyCounts[npcName] < dailyLimit;
    }

    /// <summary>
    /// 记录与某 NPC 触发了一次背词
    /// </summary>
    public void RecordNpcTrigger(string npcName)
    {
        if (NpcDailyCounts.ContainsKey(npcName))
            NpcDailyCounts[npcName]++;
        else
            NpcDailyCounts[npcName] = 1;
    }

    /// <summary>
    /// 重置每日数据（每天开始时调用）
    /// </summary>
    public void ResetDaily()
    {
        NpcDailyCounts.Clear();
        NpcFirstChatToday.Clear();
    }

    /// <summary>
    /// 开始一个新挑战
    /// </summary>
    public void StartChallenge(string prefix, int deadlineDay)
    {
        HasActiveChallenge = true;
        ChallengePrefix = prefix;
        ChallengeDeadlineDay = deadlineDay;
        ChallengeStreak = 0;
    }

    /// <summary>
    /// 挑战中答对一题
    /// </summary>
    public void ChallengeCorrect()
    {
        ChallengeStreak++;
    }

    /// <summary>
    /// 挑战失败（答错或超时）
    /// </summary>
    public void ChallengeFail(int currentDay)
    {
        HasActiveChallenge = false;
        LastChallengeDay = currentDay;
        ChallengeStreak = 0;
    }

    /// <summary>
    /// 挑战完成
    /// </summary>
    public void ChallengeComplete(int currentDay)
    {
        HasActiveChallenge = false;
        LastChallengeDay = currentDay;
        ChallengeStreak = 0;
    }

    /// <summary>
    /// 检查挑战是否在冷却中
    /// </summary>
    public bool IsChallengeOnCooldown(int currentDay, int cooldownDays)
    {
        return (currentDay - LastChallengeDay) < cooldownDays;
    }
}

/// <summary>
/// 玩家背词进度统计
/// </summary>
public class ProgressStats
{
    /// <summary>词库总词数</summary>
    public int TotalInBank { get; set; }

    /// <summary>当前错词数</summary>
    public int TotalWrong { get; set; }

    /// <summary>已掌握词数（连续答对 3 次以上）</summary>
    public int TotalMastered { get; set; }

    /// <summary>曾遇到过的词数</summary>
    public int TotalEncountered { get; set; }

    /// <summary>是否已全部掌握词库中所有单词</summary>
    public bool IsAllMastered { get; set; }

    /// <summary>未接触过的词数</summary>
    public int NeverSeen => TotalInBank - TotalEncountered;
}
