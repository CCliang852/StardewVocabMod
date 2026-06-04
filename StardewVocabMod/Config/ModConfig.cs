using StardewModdingAPI;

namespace StardewVocabMod.Config;

/// <summary>
/// Mod 配置模型，所有可配置项及其默认值
/// </summary>
public class ModConfig
{
    /// <summary>
    /// Mod 总开关。关闭后所有功能不触发。
    /// </summary>
    public bool ModEnabled { get; set; } = true;

    /// <summary>
    /// 固定学习模式触发时间（游戏时间，如 700 = 早上7:00，1400 = 下午2:00）
    /// </summary>
    public int FixedQuizTime { get; set; } = 700;

    /// <summary>
    /// 固定学习模式每次出题数量
    /// </summary>
    public int FixedQuizWordCount { get; set; } = 20;

    /// <summary>
    /// 及格阈值（正确率），范围 0.0 ~ 1.0
    /// </summary>
    public float PassThreshold { get; set; } = 0.8f;

    /// <summary>
    /// 每个村民每天 NPC 模式最大触发次数
    /// </summary>
    public int NpcDailyLimit { get; set; } = 3;

    /// <summary>
    /// 挑战模式冷却天数
    /// </summary>
    public int ChallengeCooldownDays { get; set; } = 7;

    /// <summary>
    /// 挑战模式题数
    /// </summary>
    public int ChallengeWordCount { get; set; } = 15;

    /// <summary>
    /// 挑战模式时间限制（天）
    /// </summary>
    public int ChallengeTimeLimitDays { get; set; } = 3;

    /// <summary>
    /// 验证并修正配置值，确保在合理范围内
    /// </summary>
    /// <param name="monitor">用于输出警告日志</param>
    public void Validate(IMonitor monitor)
    {
        // 时间必须是整百且在合理范围（6:00 AM ~ 2:00 AM）
        if (FixedQuizTime < 600 || FixedQuizTime > 2600 || FixedQuizTime % 100 != 0)
        {
            monitor.Log($"FixedQuizTime ({FixedQuizTime}) 无效，已修正为 700（早上7:00）", LogLevel.Warn);
            FixedQuizTime = 700;
        }

        if (FixedQuizWordCount < 3)
        {
            monitor.Log($"FixedQuizWordCount ({FixedQuizWordCount}) 太小，已修正为 3", LogLevel.Warn);
            FixedQuizWordCount = 3;
        }
        if (FixedQuizWordCount > 100)
        {
            monitor.Log($"FixedQuizWordCount ({FixedQuizWordCount}) 太大，已修正为 100", LogLevel.Warn);
            FixedQuizWordCount = 100;
        }

        if (PassThreshold < 0f)
        {
            monitor.Log($"PassThreshold ({PassThreshold}) 无效，已修正为 0", LogLevel.Warn);
            PassThreshold = 0f;
        }
        if (PassThreshold > 1f)
        {
            monitor.Log($"PassThreshold ({PassThreshold}) 无效，已修正为 1", LogLevel.Warn);
            PassThreshold = 1f;
        }

        if (NpcDailyLimit < 1)
        {
            monitor.Log($"NpcDailyLimit ({NpcDailyLimit}) 无效，已修正为 1", LogLevel.Warn);
            NpcDailyLimit = 1;
        }

        if (ChallengeWordCount < 3)
        {
            monitor.Log($"ChallengeWordCount ({ChallengeWordCount}) 太小，已修正为 3", LogLevel.Warn);
            ChallengeWordCount = 3;
        }

        if (ChallengeTimeLimitDays < 1)
        {
            monitor.Log($"ChallengeTimeLimitDays ({ChallengeTimeLimitDays}) 无效，已修正为 1", LogLevel.Warn);
            ChallengeTimeLimitDays = 1;
        }

        if (ChallengeCooldownDays < 1)
        {
            monitor.Log($"ChallengeCooldownDays ({ChallengeCooldownDays}) 无效，已修正为 1", LogLevel.Warn);
            ChallengeCooldownDays = 1;
        }
    }
}
