using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewVocabMod.Config;
using StardewVocabMod.Data;
using StardewVocabMod.Services;
using StardewVocabMod.UI;

namespace StardewVocabMod.Modes;

/// <summary>
/// 固定学习模式：每日固定时间 + 睡前触发答题，含繁忙延迟和奖励
/// </summary>
public class FixedMode
{
    private readonly ModConfig _config;
    private readonly WordBank _wordBank;
    private PlayerProgress _playerProgress;
    private readonly IMonitor _monitor;
    private readonly IModHelper _helper;

    private QuizService _quizService = null!;
    private ScheduleService _scheduleService = null!;

    /// <summary>下午答题是否正在进行（防止重复触发）</summary>
    private bool _afternoonQuizActive;

    /// <summary>睡前答题是否正在进行</summary>
    private bool _sleepQuizActive;

    public FixedMode(ModConfig config, WordBank wordBank, PlayerProgress playerProgress,
                     IMonitor monitor, IModHelper helper)
    {
        _config = config;
        _wordBank = wordBank;
        _playerProgress = playerProgress;
        _monitor = monitor;
        _helper = helper;
    }

    /// <summary>
    /// 更新 PlayerProgress 引用（切换存档时调用）
    /// </summary>
    public void UpdatePlayerProgress(PlayerProgress progress)
    {
        _playerProgress = progress;
    }

    /// <summary>
    /// 初始化服务并注册 SMAPI 事件
    /// </summary>
    public void Initialize()
    {
        _quizService = new QuizService(_wordBank);
        _scheduleService = new ScheduleService(_monitor);

        _helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        _helper.Events.GameLoop.DayEnding += OnDayEnding;

        var timeStr = $"{_config.FixedQuizTime / 100:D2}:{_config.FixedQuizTime % 100:D2}";
        _monitor.Log($"固定学习模式已就绪（{timeStr} + 睡前触发）", LogLevel.Info);
    }

    /// <summary>
    /// 重置每日状态（新的一天调用）
    /// </summary>
    public void ResetDaily()
    {
        _scheduleService.ResetDaily();
        _afternoonQuizActive = false;
        _sleepQuizActive = false;
    }

    // ==========================================
    //  时间事件
    // ==========================================

    private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        if (!_config.ModEnabled)
            return;

        // 剧情事件/节日进行中不触发
        if (Game1.eventUp || Game1.CurrentEvent != null || Game1.isFestival())
            return;

        if (_afternoonQuizActive || _sleepQuizActive)
            return;

        var newTime = e.NewTime;

        // 优先检查延迟答题是否到期
        if (_scheduleService.HasPendingQuiz && newTime >= _scheduleService.PendingQuizTime)
        {
            if (_scheduleService.TryTriggerPending(newTime))
            {
                StartQuiz("日间");
            }
            return;
        }

        // 配置的固定时间触发
        if (newTime == _config.FixedQuizTime && !_scheduleService.AfternoonQuizDone)
        {
            if (_scheduleService.TryTriggerAtTime(newTime))
            {
                StartQuiz("日间");
            }
        }
    }

    // ==========================================
    //  睡觉事件
    // ==========================================

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        if (!_config.ModEnabled)
            return;

        // 节日/事件中跳过（玩家无法在节日中正常睡觉）
        if (Game1.eventUp || Game1.CurrentEvent != null || Game1.isFestival())
            return;

        if (_sleepQuizActive)
            return;

        if (_scheduleService.SleepQuizDone)
            return;

        // 如果日间答题被延迟了但还没触发，标记为已完成
        if (_scheduleService.HasPendingQuiz)
        {
            _scheduleService.AfternoonQuizDone = true;
        }

        _scheduleService.SleepQuizDone = true;
        StartQuiz("睡前");
    }

    // ==========================================
    //  答题流程
    // ==========================================

    /// <summary>
    /// 开始一轮答题
    /// </summary>
    /// <param name="label">标签（"下午" 或 "睡前"），仅用于日志</param>
    private void StartQuiz(string label)
    {
        var isAfternoon = label == "日间";

        if (isAfternoon)
            _afternoonQuizActive = true;
        else
            _sleepQuizActive = true;

        _monitor.Log($"开始{label}答题，共 {_config.FixedQuizWordCount} 题", LogLevel.Info);

        // 生成题目
        var session = _quizService.GenerateRandomQuiz(_config.FixedQuizWordCount);
        if (session.TotalCount == 0)
        {
            _monitor.Log("词库为空，无法出题！", LogLevel.Warn);
            if (isAfternoon) _afternoonQuizActive = false;
            else _sleepQuizActive = false;
            return;
        }

        // 显示答题窗口
        var window = new VocabQuizWindow(
            session,
            onComplete: s => OnQuizComplete(s, isAfternoon),
            onClose: () => OnQuizClosed(isAfternoon),
            passThreshold: _config.PassThreshold
        );

        Game1.activeClickableMenu = window;
    }

    /// <summary>
    /// 答题完成（所有题目答完）
    /// </summary>
    private void OnQuizComplete(QuizSession session, bool isAfternoon)
    {
        _monitor.Log($"答题完成：{session.CorrectCount}/{session.TotalCount} 正确，" +
                    $"正确率 {session.Score * 100:F0}%", LogLevel.Info);

        // 记录答对/答错
        foreach (var question in session.Questions)
        {
            if (session.WrongWords.Contains(question.Word.Word))
                _playerProgress.RecordWrong(question.Word.Word);
            else
                _playerProgress.RecordCorrect(question.Word.Word);
        }

        // 发放奖励
        RewardService.GrantRewards(session.Score, session.TotalCount, session.CorrectCount, _config.PassThreshold);

        // 进度统计
        var stats = _playerProgress.GetStats(_wordBank.WordCount);
        if (stats.TotalMastered > 0)
        {
            _monitor.Log($"学习进度：已掌握 {stats.TotalMastered}/{stats.TotalInBank} 词，" +
                        $"错词 {stats.TotalWrong} 个", LogLevel.Info);
        }

        // 全通检测
        if (stats.IsAllMastered)
        {
            Game1.addHUDMessage(new HUDMessage("🎉 恭喜！词库中所有单词已全部掌握！太强了！🎉",
                HUDMessage.achievement_type));
        }
        else if (stats.TotalWrong == 0 && stats.TotalMastered > 0)
        {
            Game1.addHUDMessage(new HUDMessage("没有错词了！可以导入新词库继续学习～",
                HUDMessage.newQuest_type));
        }

        // 全对或高分时给一条鼓励消息
        if (session.Score >= 1.0f)
        {
            Game1.addHUDMessage(new HUDMessage("全对！太厉害了！🎉", HUDMessage.achievement_type));
        }
        else if (session.Score >= _config.PassThreshold)
        {
            Game1.addHUDMessage(new HUDMessage("通过！继续保持～", HUDMessage.newQuest_type));
        }
        else
        {
            Game1.addHUDMessage(new HUDMessage("正确率不足，下次加油！", HUDMessage.error_type));
        }

        // 释放活动标记
        if (isAfternoon)
            _afternoonQuizActive = false;
        else
            _sleepQuizActive = false;
    }

    /// <summary>
    /// 答题窗口被关闭（玩家主动关闭，未答完）
    /// </summary>
    private void OnQuizClosed(bool isAfternoon)
    {
        _monitor.Log("玩家关闭了答题窗口（未完成）", LogLevel.Info);

        if (isAfternoon)
            _afternoonQuizActive = false;
        else
            _sleepQuizActive = false;

        var msg = isAfternoon
            ? "日间答题已跳过，睡前还有一次机会哦～"
            : "晚安！明天也要认真学习～";
        Game1.addHUDMessage(new HUDMessage(msg, HUDMessage.newQuest_type));
    }
}
