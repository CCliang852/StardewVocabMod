using System.IO;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewVocabMod.Config;
using StardewVocabMod.Data;
using StardewVocabMod.Modes;
using StardewVocabMod.UI;

namespace StardewVocabMod;

/// <summary>
/// Mod 主入口，负责生命周期管理和模块初始化
/// </summary>
public class ModEntry : Mod
{
    private ModConfig _config = null!;
    private WordBank _wordBank = null!;
    private PlayerProgress _playerProgress = null!;
    private FixedMode? _fixedMode;
    private NpcMode? _npcMode;
    private ChallengeMode? _challengeMode;

    public override void Entry(IModHelper helper)
    {
        // 加载配置
        _config = helper.ReadConfig<ModConfig>();
        _config.Validate(Monitor);

        // 注册游戏事件
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;

        // 注册进度查看控制台命令
        helper.ConsoleCommands.Add("vocab-progress", "查看背单词进度\n用法: vocab-progress", HandleProgress);
        helper.ConsoleCommands.Add("vp", "vocab-progress 的简写", HandleProgress);

        Monitor.Log("星露谷背单词 Mod 已加载！", LogLevel.Info);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        // 初始化词库
        _wordBank = new WordBank(Monitor);

        // 加载内置 CET-6 词库
        var wordBankPath = Path.Combine(Helper.DirectoryPath, "Assets", "cet6_core.json");
        _wordBank.LoadBuiltInWords(wordBankPath);

        // 加载词关系表
        var relationPath = Path.Combine(Helper.DirectoryPath, "Assets", "word_relations.json");
        _wordBank.LoadRelations(relationPath);

        // 加载玩家自定义词库（如果存在）
        var customPath = Path.Combine(Helper.DirectoryPath, "Assets", "custom_words.json");
        if (File.Exists(customPath))
        {
            _wordBank.ImportCustomWords(customPath);
        }

        // 扫描 Mods 文件夹下的自定义 .txt 词库
        var modDir = Helper.DirectoryPath;
        var txtFiles = Directory.GetFiles(modDir, "*.txt");
        foreach (var txt in txtFiles)
        {
            var fileName = Path.GetFileName(txt);
            // 跳过 manifest 等非词库文件
            if (fileName.Contains("manifest") || fileName.Contains("readme"))
                continue;
            _wordBank.ImportCustomWords(txt);
        }

        Monitor.Log($"词库就绪: {_wordBank.WordCount} 个单词, " +
                    $"关系表覆盖 {_wordBank.RelationCount} 个词", LogLevel.Info);

        // 注册 GMCM 配置面板
        ModConfigMenu.Register(Helper, ModManifest, _config, Monitor);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        // 从存档恢复玩家进度
        _playerProgress = Helper.Data.ReadSaveData<PlayerProgress>("player-progress")
                          ?? new PlayerProgress();

        // 初始化固定学习模式（首次加载存档时创建）
        if (_fixedMode == null)
        {
            _fixedMode = new FixedMode(_config, _wordBank, _playerProgress, Monitor, Helper);
            _fixedMode.Initialize();
        }
        else
        {
            _fixedMode.UpdatePlayerProgress(_playerProgress);
        }

        // 初始化 NPC 交互模式（首次加载存档时创建）
        if (_npcMode == null)
        {
            _npcMode = new NpcMode(_config, _wordBank, _playerProgress, Monitor, Helper);
            _npcMode.Initialize();
        }
        else
        {
            _npcMode.UpdatePlayerProgress(_playerProgress);
        }

        // 初始化挑战模式（首次加载存档时创建）
        if (_challengeMode == null)
        {
            _challengeMode = new ChallengeMode(_config, _wordBank, _playerProgress, Monitor, Helper);
            _challengeMode.Initialize();
        }
        else
        {
            _challengeMode.UpdatePlayerProgress(_playerProgress);
        }

        Monitor.Log("玩家进度已加载。", LogLevel.Info);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        // 重置每日计数器（NPC 互动次数等）
        _playerProgress.ResetDaily();
        _fixedMode?.ResetDaily();
        Monitor.Log("新的一天，每日数据已重置。", LogLevel.Info);
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        // 返回标题画面时保存数据
        Helper.Data.WriteSaveData("player-progress", _playerProgress);
        Monitor.Log("玩家进度已保存。", LogLevel.Info);
    }

    /// <summary>
    /// 获取当前配置
    /// </summary>
    public ModConfig GetConfig() => _config;

    /// <summary>
    /// 获取词库实例
    /// </summary>
    public WordBank GetWordBank() => _wordBank;

    /// <summary>
    /// 获取玩家进度
    /// </summary>
    public PlayerProgress GetPlayerProgress() => _playerProgress;

    /// <summary>
    /// 控制台命令：查看背单词进度
    /// </summary>
    private void HandleProgress(string command, string[] args)
    {
        if (_wordBank == null || _wordBank.WordCount == 0)
        {
            Monitor.Log("词库尚未加载，请先加载存档。", LogLevel.Warn);
            return;
        }

        if (_playerProgress == null)
        {
            Monitor.Log("玩家数据尚未加载，请先加载存档。", LogLevel.Warn);
            return;
        }

        var stats = _playerProgress.GetStats(_wordBank.WordCount);
        var pct = stats.TotalInBank > 0
            ? (float)stats.TotalMastered / stats.TotalInBank * 100f
            : 0f;

        Monitor.Log("══════════ 背单词进度 ══════════", LogLevel.Info);
        Monitor.Log($"  词库总量: {stats.TotalInBank} 词", LogLevel.Info);
        Monitor.Log($"  已掌握:   {stats.TotalMastered} 词 ({pct:F1}%)", LogLevel.Info);
        Monitor.Log($"  曾遇到:   {stats.TotalEncountered} 词", LogLevel.Info);
        Monitor.Log($"  未接触:   {stats.NeverSeen} 词", LogLevel.Info);
        Monitor.Log($"  错词中:   {stats.TotalWrong} 词", LogLevel.Info);
        Monitor.Log("════════════════════════════════", LogLevel.Info);

        if (stats.IsAllMastered)
        {
            Monitor.Log("🎉 恭喜！词库所有单词已全部掌握！可以导入新词库继续学习。", LogLevel.Info);
        }
        else if (stats.TotalWrong == 0 && stats.TotalMastered > 0)
        {
            Monitor.Log("✅ 当前无错词，继续保持！", LogLevel.Info);
        }
    }
}
