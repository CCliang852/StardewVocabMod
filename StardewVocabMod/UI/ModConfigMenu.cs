using System;
using StardewModdingAPI;
using StardewVocabMod.Config;

namespace StardewVocabMod.UI;

/// <summary>
/// GMCM（Generic Mod Config Menu）集成：在游戏设置菜单中提供 Mod 配置面板
/// </summary>
public static class ModConfigMenu
{
    /// <summary>
    /// 向 GMCM 注册配置面板。如果 GMCM 未安装则跳过。
    /// </summary>
    public static void Register(IModHelper helper, IManifest manifest, ModConfig config, IMonitor monitor)
    {
        // 获取 GMCM API
        var api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api == null)
        {
            monitor.Log("未检测到 Generic Mod Config Menu，跳过配置面板注册。", LogLevel.Info);
            return;
        }

        // 注册 Mod 配置
        api.Register(
            mod: manifest,
            reset: () =>
            {
                config.ModEnabled = true;
                config.FixedQuizTime = 700;
                config.FixedQuizWordCount = 20;
                config.PassThreshold = 0.8f;
                config.NpcDailyLimit = 3;
                config.ChallengeWordCount = 15;
                config.ChallengeTimeLimitDays = 3;
                config.ChallengeCooldownDays = 7;
            },
            save: () =>
            {
                helper.WriteConfig(config);
                monitor.Log("Mod 配置已保存。", LogLevel.Info);
            },
            titleScreenOnly: false
        );

        // ===== 通用设置 =====
        api.AddSectionTitle(manifest, () => "🔧 通用设置");

        api.AddBoolOption(
            mod: manifest,
            getValue: () => config.ModEnabled,
            setValue: val => config.ModEnabled = val,
            name: () => "启用 Mod",
            tooltip: () => "关闭后所有背单词功能暂停，不影响游戏正常进行。"
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => (int)(config.PassThreshold * 100),
            setValue: val => config.PassThreshold = val / 100f,
            name: () => "通过阈值 (%)",
            tooltip: () => "正确率达到此百分比才算通过，可获得奖励。",
            min: 50,
            max: 100,
            interval: 5,
            formatValue: val => $"{val}%"
        );

        // ===== 固定学习模式 =====
        api.AddSectionTitle(manifest, () => "📖 固定学习模式");

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.FixedQuizTime,
            setValue: val => config.FixedQuizTime = val,
            name: () => "触发时间",
            tooltip: () => "每天固定触发答题的游戏时间（24小时制，如 700 = 早上7:00，1400 = 下午2:00）。",
            min: 600,
            max: 2600,
            interval: 100,
            formatValue: val => $"{val / 100:D2}:{val % 100:D2}"
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.FixedQuizWordCount,
            setValue: val => config.FixedQuizWordCount = val,
            name: () => "每次题数",
            tooltip: () => "固定触发和睡前答题的题目数量。",
            min: 5,
            max: 50,
            interval: 5
        );

        // ===== NPC 交互模式 =====
        api.AddSectionTitle(manifest, () => "💬 NPC 交互模式");

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.NpcDailyLimit,
            setValue: val => config.NpcDailyLimit = val,
            name: () => "每村民每日上限",
            tooltip: () => "同一个村民每天最多触发答题的次数（跳过首次闲聊）。",
            min: 1,
            max: 10,
            interval: 1
        );

        // ===== 挑战模式 =====
        api.AddSectionTitle(manifest, () => "🏆 挑战模式");

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.ChallengeWordCount,
            setValue: val => config.ChallengeWordCount = val,
            name: () => "挑战题数",
            tooltip: () => "每次词根挑战的题目数量。",
            min: 5,
            max: 30,
            interval: 5
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.ChallengeTimeLimitDays,
            setValue: val => config.ChallengeTimeLimitDays = val,
            name: () => "挑战时限（天）",
            tooltip: () => "接受挑战后必须在几天内完成。",
            min: 1,
            max: 14,
            interval: 1
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.ChallengeCooldownDays,
            setValue: val => config.ChallengeCooldownDays = val,
            name: () => "挑战冷却（天）",
            tooltip: () => "挑战完成/失败后需等待几天才能再次接受。",
            min: 1,
            max: 28,
            interval: 1
        );

        monitor.Log("GMCM 配置面板已注册。", LogLevel.Info);
    }
}

// ===============================================================
//  GMCM API 接口定义（内联，不依赖 GMCM DLL）
//  参考：https://github.com/spacechase0/StardewValleyMods
// ===============================================================

/// <summary>
/// GMCM 提供的 API 接口。此处仅声明本 Mod 使用到的方法。
/// </summary>
public interface IGenericModConfigMenuApi
{
    /// <summary>注册 Mod 配置页面</summary>
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    /// <summary>添加分节标题</summary>
    void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

    /// <summary>添加布尔开关选项</summary>
    void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name,
        Func<string>? tooltip = null, string? fieldId = null);

    /// <summary>添加整数滑块选项</summary>
    void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name,
        Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null,
        Func<int, string>? formatValue = null, string? fieldId = null);
}
