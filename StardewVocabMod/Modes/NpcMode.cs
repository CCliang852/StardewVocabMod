using System;
using System.Collections.Generic;
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
/// NPC 交互模式：对话结束后触发错词复习，答对加好感
/// </summary>
public class NpcMode
{
    // ===== 常量 =====
    private const int NpcQuestionCount = 5;

    // ===== 依赖 =====
    private readonly ModConfig _config;
    private readonly WordBank _wordBank;
    private PlayerProgress _playerProgress;
    private readonly IMonitor _monitor;
    private readonly IModHelper _helper;

    private QuizService _quizService = null!;

    // ===== 状态 =====
    private NPC? _lastTalkedNpc;
    private bool _quizInProgress;
    private bool _skipCurrentConversation;

    // ===== NPC 台词缓存 =====
    private Dictionary<string, List<string>> _npcSarcasmLines = new();
    private Dictionary<string, List<string>> _npcPraiseLines = new();
    private readonly List<string> _defaultSarcasm = new()
    {
        "……你确定？",
        "这个词不难吧？",
        "emmm，再想想？",
        "哈？这都能错？",
        "……回去多背背吧。"
    };
    private readonly List<string> _defaultPraise = new()
    {
        "不错嘛！",
        "答对了，厉害！",
        "看来你有在认真学习。",
        "很好！",
        "哇，你居然会这个！"
    };

    public NpcMode(ModConfig config, WordBank wordBank, PlayerProgress playerProgress,
                   IMonitor monitor, IModHelper helper)
    {
        _config = config;
        _wordBank = wordBank;
        _playerProgress = playerProgress;
        _monitor = monitor;
        _helper = helper;
    }

    /// <summary>
    /// 初始化服务并注册事件
    /// </summary>
    public void Initialize()
    {
        _quizService = new QuizService(_wordBank);
        _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        LoadNpcLines();
        _monitor.Log("NPC 交互模式已就绪", LogLevel.Info);
    }

    /// <summary>
    /// 更新 PlayerProgress 引用（切换存档时调用）
    /// </summary>
    public void UpdatePlayerProgress(PlayerProgress progress)
    {
        _playerProgress = progress;
    }

    // ==========================================
    //  台词加载
    // ==========================================

    private void LoadNpcLines()
    {
        try
        {
            var path = System.IO.Path.Combine(_helper.DirectoryPath, "Assets", "npc_lines.json");
            if (!System.IO.File.Exists(path))
            {
                _monitor.Log("NPC 台词文件不存在，使用默认台词", LogLevel.Info);
                return;
            }

            var json = System.IO.File.ReadAllText(path);
            var data = System.Text.Json.JsonSerializer.Deserialize<NpcLinesData>(json);
            if (data != null)
            {
                _npcSarcasmLines = data.Sarcasm ?? new();
                _npcPraiseLines = data.Praise ?? new();
                _monitor.Log($"NPC 台词已加载：{_npcSarcasmLines.Count} 位 NPC 的台词", LogLevel.Info);
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"NPC 台词加载失败：{ex.Message}，使用默认台词", LogLevel.Warn);
        }
    }

    // ==========================================
    //  对话检测
    // ==========================================

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!_config.ModEnabled || _quizInProgress)
            return;

        // 用 Game1.currentSpeaker 检测当前对话对象
        var currentNpc = Game1.currentSpeaker as NPC;

        // 检测对话开始
        if (_lastTalkedNpc == null && currentNpc != null)
        {
            // 判断是否为今日首次对话
            if (!_playerProgress.NpcFirstChatToday.Contains(currentNpc.Name))
            {
                // 今天第一次 → 标记到集合，本次不触发答题
                _playerProgress.NpcFirstChatToday.Add(currentNpc.Name);
                _skipCurrentConversation = true;
            }
            else
            {
                // 今天第二+次 → 允许触发答题
                _skipCurrentConversation = false;
            }
        }

        // 检测对话结束：currentSpeaker 从 NPC 变为 null/非NPC
        if (_lastTalkedNpc != null && currentNpc == null)
        {
            var npc = _lastTalkedNpc;
            _lastTalkedNpc = null;

            if (!_skipCurrentConversation)
            {
                TryTriggerQuiz(npc);
            }
            _skipCurrentConversation = false;
        }

        _lastTalkedNpc = currentNpc;
    }

    // ==========================================
    //  触发判断
    // ==========================================

    /// <summary>
    /// 检查所有跳过条件，通过则触发答题
    /// </summary>
    private void TryTriggerQuiz(NPC npc)
    {
        // 1. 跳过：生日
        if (npc.isBirthday())
        {
            _monitor.Log($"{npc.Name} 今天生日，跳过背词", LogLevel.Info);
            return;
        }

        // 2. 跳过：节日
        if (Game1.isFestival())
        {
            return;
        }

        // 3. 跳过：剧情事件进行中
        if (Game1.eventUp || Game1.CurrentEvent != null)
        {
            _monitor.Log("剧情事件进行中，跳过背词", LogLevel.Info);
            return;
        }

        // 4. 跳过：无法识别 NPC（非村民，如动物、怪物等）
        if (!npc.IsVillager)
        {
            return;
        }

        // 5. 检查每日限制
        if (!_playerProgress.CanTriggerNpcQuiz(npc.Name, _config.NpcDailyLimit))
        {
            _monitor.Log($"{npc.Name} 今日答题次数已达上限", LogLevel.Info);
            return;
        }

        // 通过所有检查，触发答题
        _playerProgress.RecordNpcTrigger(npc.Name);
        StartNpcQuiz(npc);
    }

    // ==========================================
    //  答题流程
    // ==========================================

    /// <summary>
    /// 开始 NPC 答题
    /// </summary>
    private void StartNpcQuiz(NPC npc)
    {
        _quizInProgress = true;
        _monitor.Log($"与 {npc.Name} 开始背词复习", LogLevel.Info);

        // 生成复习题目：错词优先
        var wrongWords = _playerProgress.GetWrongWords(20);
        var session = _quizService.GenerateReviewQuiz(wrongWords, NpcQuestionCount);

        if (session.TotalCount == 0)
        {
            // 没有错词也没有词库（极端情况）
            _monitor.Log("无可用的复习题目", LogLevel.Warn);
            _quizInProgress = false;
            return;
        }

        // 显示答题窗口
        var window = new VocabQuizWindow(
            session,
            onComplete: s => OnNpcQuizComplete(s, npc),
            onClose: () => OnNpcQuizClosed(npc),
            passThreshold: _config.PassThreshold
        );

        Game1.activeClickableMenu = window;
    }

    /// <summary>
    /// 答题完成
    /// </summary>
    private void OnNpcQuizComplete(QuizSession session, NPC npc)
    {
        // 记录进度
        foreach (var question in session.Questions)
        {
            if (session.WrongWords.Contains(question.Word.Word))
                _playerProgress.RecordWrong(question.Word.Word);
            else
                _playerProgress.RecordCorrect(question.Word.Word);
        }

        // 好感度修改：每答对一题 +1（changeFriendship 内部自动处理上限）
        var correctCount = session.CorrectCount;
        if (correctCount > 0)
        {
            Game1.player.changeFriendship(correctCount, npc);
        }

        // NPC 台词
        var score = session.Score;
        string npcMessage;
        if (score >= _config.PassThreshold)
        {
            npcMessage = GetRandomLine(_npcPraiseLines, npc.Name, _defaultPraise);
        }
        else
        {
            npcMessage = GetRandomLine(_npcSarcasmLines, npc.Name, _defaultSarcasm);
        }

        // 显示 NPC 对话气泡
        npc.showTextAboveHead(npcMessage, duration: 3000);

        // HUD 提示：检查好友度是否已达上限
        var currentHearts = Game1.player.getFriendshipHeartLevelForNPC(npc.Name);
        var atMaxHearts = currentHearts >= 10; // 10 心为大多数 NPC 的上限
        var hudMsg = correctCount > 0
            ? (atMaxHearts
                ? $"{npc.Name} 好感度已达上限，继续保持！"
                : $"{npc.Name} 好感度 +{correctCount}")
            : $"{npc.Name}：还需努力……";
        Game1.addHUDMessage(new HUDMessage(hudMsg, HUDMessage.newQuest_type));

        _monitor.Log($"{npc.Name} 答题完成：{correctCount}/{session.TotalCount} 正确", LogLevel.Info);
        _quizInProgress = false;
    }

    /// <summary>
    /// 答题窗口被提前关闭
    /// </summary>
    private void OnNpcQuizClosed(NPC npc)
    {
        _monitor.Log($"与 {npc.Name} 的答题被跳过", LogLevel.Info);
        npc.showTextAboveHead("下次再聊吧～", duration: 2000);
        _quizInProgress = false;
    }

    // ==========================================
    //  台词工具
    // ==========================================

    private string GetRandomLine(Dictionary<string, List<string>> pool, string npcName,
                                  List<string> defaults)
    {
        if (pool.TryGetValue(npcName, out var lines) && lines.Count > 0)
            return lines[new Random().Next(lines.Count)];
        return defaults[new Random().Next(defaults.Count)];
    }
}

/// <summary>
/// NPC 台词 JSON 数据结构
/// </summary>
internal class NpcLinesData
{
    public Dictionary<string, List<string>> Sarcasm { get; set; } = new();
    public Dictionary<string, List<string>> Praise { get; set; } = new();
}
