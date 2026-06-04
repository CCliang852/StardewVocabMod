using System;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewVocabMod.Config;
using StardewVocabMod.Data;
using StardewVocabMod.Services;
using StardewVocabMod.UI;

namespace StardewVocabMod.Modes;

/// <summary>
/// 挑战模式：公告栏词根挑战。同词根 15 题，3 天内完成，奖励 3 个洒水器。
/// </summary>
public class ChallengeMode
{
    // ===== 常量 =====
    private const int SprinklerItemId = 621;         // 优质洒水器

    // ===== 依赖 =====
    private readonly ModConfig _config;
    private readonly WordBank _wordBank;
    private PlayerProgress _playerProgress;
    private readonly IMonitor _monitor;
    private readonly IModHelper _helper;

    private QuizService _quizService = null!;
    private string _currentPrefix = string.Empty;
    private bool _challengeWindowOpen;

    public ChallengeMode(ModConfig config, WordBank wordBank, PlayerProgress playerProgress,
                         IMonitor monitor, IModHelper helper)
    {
        _config = config;
        _wordBank = wordBank;
        _playerProgress = playerProgress;
        _monitor = monitor;
        _helper = helper;
    }

    /// <summary>
    /// 初始化服务、注册控制台命令和事件
    /// </summary>
    public void Initialize()
    {
        _quizService = new QuizService(_wordBank);

        // 注册控制台命令
        _helper.ConsoleCommands.Add("vocab-challenge", "查看或接受词根挑战\n用法: vocab-challenge [accept|status]", HandleCommand);
        _helper.ConsoleCommands.Add("vc", "vocab-challenge 的简写", HandleCommand);

        // 监听新的一天：检查挑战超时 + 冷却通知
        _helper.Events.GameLoop.DayStarted += OnDayStarted;

        _monitor.Log("挑战模式已就绪（公告栏词根挑战）", LogLevel.Info);
        _monitor.Log("在 SMAPI 控制台输入 vocab-challenge 或 vc 开始挑战", LogLevel.Info);
    }

    public void UpdatePlayerProgress(PlayerProgress progress)
    {
        _playerProgress = progress;
    }

    // ==========================================
    //  控制台命令
    // ==========================================

    private void HandleCommand(string command, string[] args)
    {
        var action = args.Length > 0 ? args[0].ToLower() : "status";

        switch (action)
        {
            case "accept":
            case "start":
                AcceptChallenge();
                break;
            case "status":
            case "info":
            default:
                ShowStatus();
                break;
        }
    }

    private void ShowStatus()
    {
        var today = Game1.Date.TotalDays;

        if (_playerProgress.HasActiveChallenge)
        {
            var remaining = _playerProgress.ChallengeDeadlineDay - today;
            _monitor.Log($"当前挑战进行中！词根: {_playerProgress.ChallengePrefix}-，" +
                        $"已完成: {_playerProgress.ChallengeStreak}/{_config.ChallengeWordCount} 题，" +
                        $"剩余: {remaining} 天", LogLevel.Info);
        }
        else if (_playerProgress.IsChallengeOnCooldown(today, _config.ChallengeCooldownDays))
        {
            var remaining = _config.ChallengeCooldownDays - (today - _playerProgress.LastChallengeDay);
            _monitor.Log($"挑战冷却中，还需等待 {remaining} 天", LogLevel.Info);
        }
        else
        {
            var prefixes = _wordBank.GetAvailablePrefixes(_config.ChallengeWordCount);
            _monitor.Log($"挑战可用！可选的词根前缀: {string.Join(", ", prefixes.Take(5))}", LogLevel.Info);
            _monitor.Log("输入 vocab-challenge accept 接受挑战", LogLevel.Info);
        }
    }

    // ==========================================
    //  接受挑战
    // ==========================================

    private void AcceptChallenge()
    {
        if (!_config.ModEnabled)
        {
            _monitor.Log("Mod 未启用，请先在配置中开启", LogLevel.Warn);
            return;
        }

        var today = Game1.Date.TotalDays;

        // 检查是否有进行中的挑战
        if (_playerProgress.HasActiveChallenge)
        {
            _monitor.Log($"已有进行中的挑战（词根: {_playerProgress.ChallengePrefix}-），" +
                        $"输入 vocab-challenge status 查看进度", LogLevel.Info);
            return;
        }

        // 检查冷却
        if (_playerProgress.IsChallengeOnCooldown(today, _config.ChallengeCooldownDays))
        {
            var remaining = _config.ChallengeCooldownDays - (today - _playerProgress.LastChallengeDay);
            _monitor.Log($"挑战冷却中，还需等待 {remaining} 天", LogLevel.Info);
            return;
        }

        // 获取可用前缀
        var prefixes = _wordBank.GetAvailablePrefixes(_config.ChallengeWordCount);
        if (prefixes.Count == 0)
        {
            _monitor.Log("词库中没有足够的同词根单词用于挑战", LogLevel.Warn);
            return;
        }

        // 随机选一个词根前缀
        _currentPrefix = prefixes[new Random().Next(prefixes.Count)];
        var deadline = today + _config.ChallengeTimeLimitDays;

        // 开始挑战
        _playerProgress.StartChallenge(_currentPrefix, deadline);
        _monitor.Log($"词根挑战已接受！前缀: {_currentPrefix}-，共 {_config.ChallengeWordCount} 题，" +
                    $"截止日期: {_config.ChallengeTimeLimitDays} 天后", LogLevel.Info);

        // 显示答题窗口
        StartChallengeQuiz();
    }

    // ==========================================
    //  答题窗口
    // ==========================================

    private void StartChallengeQuiz()
    {
        if (_challengeWindowOpen)
            return;

        _challengeWindowOpen = true;

        var session = _quizService.GenerateChallengeQuiz(_playerProgress.ChallengePrefix, _config.ChallengeWordCount);
        if (session.TotalCount == 0)
        {
            _monitor.Log("无法生成挑战题目（词根单词不足）", LogLevel.Warn);
            _playerProgress.ChallengeFail(Game1.Date.TotalDays);
            _challengeWindowOpen = false;
            return;
        }

        var window = new VocabQuizWindow(
            session,
            onComplete: OnChallengeComplete,
            onClose: OnChallengeClosed,
            passThreshold: _config.PassThreshold
        );

        Game1.activeClickableMenu = window;
    }

    private void OnChallengeComplete(QuizSession session)
    {
        _challengeWindowOpen = false;
        var today = Game1.Date.TotalDays;

        // 记录进度
        foreach (var question in session.Questions)
        {
            if (session.WrongWords.Contains(question.Word.Word))
                _playerProgress.RecordWrong(question.Word.Word);
            else
                _playerProgress.RecordCorrect(question.Word.Word);
        }

        // 更新挑战连胜
        _playerProgress.ChallengeCorrect();

        var score = session.Score;
        var passed = score >= _config.PassThreshold;

        if (passed)
        {
            _playerProgress.ChallengeComplete(today);

            // 发放奖励：3 个优质洒水器
            for (var i = 0; i < 3; i++)
            {
                var sprinkler = new StardewValley.Object(SprinklerItemId.ToString(), 1);
                if (!Game1.player.addItemToInventoryBool(sprinkler))
                {
                    Game1.createItemDebris(sprinkler, Game1.player.getStandingPosition(), -1);
                }
            }

            Game1.addHUDMessage(new HUDMessage("挑战完成！获得 3 个优质洒水器！🎉", HUDMessage.achievement_type));
            _monitor.Log($"挑战成功！正确率 {score * 100:F0}%，获得 3 个优质洒水器", LogLevel.Info);

            // 显示奖励通知
            RewardNotification.Show(new System.Collections.Generic.List<RewardItem>
            {
                new() { Name = "优质洒水器", Quantity = 3 }
            });
        }
        else
        {
            _playerProgress.ChallengeFail(today);
            Game1.addHUDMessage(new HUDMessage($"挑战失败，正确率 {score * 100:F0}%，不足 {_config.PassThreshold * 100:F0}%。{_config.ChallengeCooldownDays} 天后可再试。", HUDMessage.error_type));
            _monitor.Log($"挑战失败，正确率 {score * 100:F0}%", LogLevel.Info);
        }
    }

    private void OnChallengeClosed()
    {
        _challengeWindowOpen = false;

        // 提前关闭不算失败，保留进行中的挑战（可重新打开继续）
        var today = Game1.Date.TotalDays;
        var remaining = _playerProgress.ChallengeDeadlineDay - today;

        Game1.addHUDMessage(new HUDMessage($"挑战暂停。输入 vocab-challenge accept 继续。" +
            $"剩余 {remaining} 天。", HUDMessage.newQuest_type));
    }

    // ==========================================
    //  每日检查
    // ==========================================

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!_config.ModEnabled)
            return;

        var today = Game1.Date.TotalDays;

        // 检查进行中的挑战是否超时
        if (_playerProgress.HasActiveChallenge)
        {
            if (today > _playerProgress.ChallengeDeadlineDay)
            {
                _playerProgress.ChallengeFail(today);
                _monitor.Log("挑战已超时，标记为失败", LogLevel.Info);
                Game1.addHUDMessage(new HUDMessage($"词根挑战已超时！{_config.ChallengeCooldownDays} 天后可重新挑战。", HUDMessage.error_type));
            }
            else
            {
                var remaining = _playerProgress.ChallengeDeadlineDay - today;
                Game1.addHUDMessage(new HUDMessage(
                    $"词根挑战进行中：{_playerProgress.ChallengePrefix}- 词根，" +
                    $"剩余 {remaining} 天。输入 vocab-challenge accept 继续！", HUDMessage.newQuest_type));
            }
            return;
        }

        // 检查冷却状态，如果冷却已过则通知
        if (!_playerProgress.IsChallengeOnCooldown(today, _config.ChallengeCooldownDays)
            && _playerProgress.LastChallengeDay > -999)
        {
            Game1.addHUDMessage(new HUDMessage(
                "公告栏有新挑战！输入 vocab-challenge accept 接受词根挑战！", HUDMessage.newQuest_type));
            _monitor.Log("挑战冷却已结束，玩家可接受新挑战", LogLevel.Info);
        }
    }
}
