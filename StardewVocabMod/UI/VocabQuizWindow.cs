using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using StardewVocabMod.Services;

namespace StardewVocabMod.UI;

/// <summary>
/// 通用答题窗口：显示英文单词 + 3 个中文选项，供所有模式复用
/// </summary>
public class VocabQuizWindow : IClickableMenu
{
    // ===== 布局常量 =====
    private const int WindowWidth = 680;
    private const int WindowHeight = 480;
    private const int OptionButtonWidth = 560;
    private const int OptionButtonHeight = 56;
    private const int OptionSpacing = 14;
    private const int FeedbackDurationMs = 700;

    // ===== 答题数据 =====
    private readonly QuizSession _session;
    private readonly Action<QuizSession>? _onComplete;
    private readonly Action? _onClose;
    private readonly float _passThreshold;

    // ===== UI 状态 =====
    private enum UIState { ShowingQuestion, ShowingFeedback, ShowingResults }
    private UIState _state = UIState.ShowingQuestion;
    private bool _lastAnswerCorrect;
    private int _lastSelectedIndex = -1;
    private QuizQuestion? _feedbackQuestion; // 反馈阶段显示的题目（防止 SubmitAnswer 后 CurrentIndex 变化导致提前露出下一题答案）
    private double _feedbackTimer;

    // ===== 选项按钮区域（缓存） =====
    private readonly List<Rectangle> _optionBounds = new();

    // ===== 关闭按钮区域 =====
    private Rectangle _closeBounds;

    // ===== 结果显示按钮 =====
    private Rectangle _confirmBounds;

    public VocabQuizWindow(
        QuizSession session,
        Action<QuizSession>? onComplete = null,
        Action? onClose = null,
        float passThreshold = 0.8f)
        : base(0, 0, WindowWidth, WindowHeight)
    {
        _session = session;
        _onComplete = onComplete;
        _onClose = onClose;
        _passThreshold = passThreshold;

        // 使用游戏内置的居中定位（自动处理 UI 缩放和 viewport）
        var centerPos = StardewValley.Utility.getTopLeftPositionForCenteringOnScreen(WindowWidth, WindowHeight);
        xPositionOnScreen = (int)centerPos.X;
        yPositionOnScreen = (int)centerPos.Y;

        // 计算三个选项按钮的位置
        RecalculateBounds();

        // 关闭按钮：右上角
        _closeBounds = new Rectangle(
            xPositionOnScreen + WindowWidth - 48,
            yPositionOnScreen + 12,
            32, 32);

        // 确认按钮（结果页用，稍后初始化）
        _confirmBounds = Rectangle.Empty;

        // 暂停游戏时间
        Game1.playSound("bigSelect");
    }

    private void RecalculateBounds()
    {
        _optionBounds.Clear();
        var startY = yPositionOnScreen + 190;
        var startX = xPositionOnScreen + (WindowWidth - OptionButtonWidth) / 2;

        for (var i = 0; i < 3; i++)
        {
            _optionBounds.Add(new Rectangle(
                startX,
                startY + i * (OptionButtonHeight + OptionSpacing),
                OptionButtonWidth,
                OptionButtonHeight));
        }
    }

    // ==========================================
    //  绘制
    // ==========================================

    public override void draw(SpriteBatch b)
    {
        // 半透明背景遮罩
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);

        // 主窗口背景
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, WindowWidth, WindowHeight, Color.White);

        // 标题栏
        DrawTitleBar(b);

        switch (_state)
        {
            case UIState.ShowingQuestion:
            case UIState.ShowingFeedback:
                DrawQuestionContent(b);
                if (_state == UIState.ShowingFeedback)
                    DrawFeedbackOverlay(b);
                break;
            case UIState.ShowingResults:
                DrawResultsContent(b);
                break;
        }

        drawMouse(b);
    }

    /// <summary>
    /// 标题栏：Mod 名称 + 关闭按钮
    /// </summary>
    private void DrawTitleBar(SpriteBatch b)
    {
        const string title = "星露谷背单词";
        var titleSize = Game1.smallFont.MeasureString(title);
        b.DrawString(Game1.smallFont, title,
            new Vector2(xPositionOnScreen + 18, yPositionOnScreen + 14),
            Color.DarkGoldenrod);

        // 关闭按钮
        drawTextureBox(b, _closeBounds.X, _closeBounds.Y, _closeBounds.Width, _closeBounds.Height, Color.White);
        var xSize = Game1.smallFont.MeasureString("✕");
        b.DrawString(Game1.smallFont, "✕",
            new Vector2(
                _closeBounds.X + (_closeBounds.Width - xSize.X) / 2f,
                _closeBounds.Y + (_closeBounds.Height - xSize.Y) / 2f),
            Color.DarkRed);
    }

    /// <summary>
    /// 题目内容：单词 + 选项 + 进度
    /// </summary>
    private void DrawQuestionContent(SpriteBatch b)
    {
        // 反馈阶段使用提交前的题目，防止 SubmitAnswer 后 CurrentIndex 变化露出下一题答案
        var question = _state == UIState.ShowingFeedback ? _feedbackQuestion : _session.GetCurrentQuestion();
        if (question == null) return;

        // 英文单词（大字居中）
        var wordText = question.Word.Word;
        var wordSize = Game1.dialogueFont.MeasureString(wordText);
        var wordPos = new Vector2(
            xPositionOnScreen + (WindowWidth - wordSize.X) / 2f,
            yPositionOnScreen + 60);
        b.DrawString(Game1.dialogueFont, wordText, wordPos, Color.DarkSlateBlue);

        // 词根/难度标签
        if (!string.IsNullOrEmpty(question.Word.RootPrefix))
        {
            var tag = $"词根: {question.Word.RootPrefix}";
            b.DrawString(Game1.smallFont, tag,
                new Vector2(xPositionOnScreen + 30, yPositionOnScreen + 120),
                Color.Gray);
        }

        // 三个选项按钮
        for (var i = 0; i < _optionBounds.Count; i++)
        {
            var bounds = _optionBounds[i];
            var defaultColor = Color.Wheat;

            // 悬停高亮
            if (_state == UIState.ShowingQuestion
                && bounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
            {
                defaultColor = Color.LightGoldenrodYellow;
            }

            // 反馈着色（答对/答错后）
            if (_state == UIState.ShowingFeedback)
            {
                var correctIdx = question.CorrectIndex;
                if (i == correctIdx)
                    defaultColor = Color.LightGreen;
                else if (i == _lastSelectedIndex && !_lastAnswerCorrect)
                    defaultColor = Color.LightPink;
            }

            drawTextureBox(b, bounds.X, bounds.Y, bounds.Width, bounds.Height, defaultColor);

            // 选项文本
            if (i < question.Options.Count)
            {
                var label = $"{(char)('A' + i)}. {question.Options[i]}";
                var labelSize = Game1.smallFont.MeasureString(label);
                b.DrawString(Game1.smallFont, label,
                    new Vector2(
                        bounds.X + (bounds.Width - labelSize.X) / 2f,
                        bounds.Y + (bounds.Height - labelSize.Y) / 2f),
                    Color.Black);
            }
        }

        // 底部进度和得分
        DrawProgress(b);
    }

    /// <summary>
    /// 底部进度条 + 得分信息
    /// </summary>
    private void DrawProgress(SpriteBatch b)
    {
        var progressY = yPositionOnScreen + WindowHeight - 55;
        var progressText = $"第 {Math.Min(_session.CurrentIndex + 1, _session.TotalCount)} / {_session.TotalCount} 题";
        b.DrawString(Game1.smallFont, progressText,
            new Vector2(xPositionOnScreen + 30, progressY),
            Color.DarkSlateGray);

        var scoreText = $"✅ {_session.CorrectCount} 题  ❌ {_session.CurrentIndex - _session.CorrectCount} 题";
        b.DrawString(Game1.smallFont, scoreText,
            new Vector2(xPositionOnScreen + WindowWidth - 200, progressY),
            Color.DarkSlateGray);
    }

    /// <summary>
    /// 答对/答错反馈覆盖层
    /// </summary>
    private void DrawFeedbackOverlay(SpriteBatch b)
    {
        var msg = _lastAnswerCorrect ? "✓ 回答正确！" : "✗ 回答错误";
        var color = _lastAnswerCorrect ? Color.DarkGreen : Color.DarkRed;
        var msgSize = Game1.smallFont.MeasureString(msg);

        b.DrawString(Game1.smallFont, msg,
            new Vector2(
                xPositionOnScreen + (WindowWidth - msgSize.X) / 2f,
                yPositionOnScreen + 155),
            color, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// 结算画面：得分、通过/未通过、关闭按钮
    /// </summary>
    private void DrawResultsContent(SpriteBatch b)
    {
        var centerX = xPositionOnScreen + WindowWidth / 2;

        // 标题
        const string title = "答题结束";
        var titleSize = Game1.dialogueFont.MeasureString(title);
        b.DrawString(Game1.dialogueFont, title,
            new Vector2(centerX - titleSize.X / 2f, yPositionOnScreen + 50),
            Color.DarkSlateBlue);

        // 得分（百分比 + 分数）
        var percentText = $"正确率: {_session.Score * 100:F0}%";
        var percentSize = Game1.dialogueFont.MeasureString(percentText);
        b.DrawString(Game1.dialogueFont, percentText,
            new Vector2(centerX - percentSize.X / 2f, yPositionOnScreen + 130),
            _session.Score >= _passThreshold ? Color.DarkGreen : Color.DarkOrange);

        // 详细统计
        var detail = $"共 {_session.TotalCount} 题，答对 {_session.CorrectCount} 题，答错 {_session.TotalCount - _session.CorrectCount} 题";
        var detailSize = Game1.smallFont.MeasureString(detail);
        b.DrawString(Game1.smallFont, detail,
            new Vector2(centerX - detailSize.X / 2f, yPositionOnScreen + 200),
            Color.DarkSlateGray);

        // 错词列表（如果有的话）
        if (_session.WrongWords.Count > 0)
        {
            var wrongText = $"错词: {string.Join("、", _session.WrongWords)}";
            // 如果太长大致显示
            if (wrongText.Length > 50)
                wrongText = wrongText[..50] + "…";
            b.DrawString(Game1.smallFont, wrongText,
                new Vector2(xPositionOnScreen + 30, yPositionOnScreen + 240),
                Color.IndianRed);
        }

        // 确认按钮
        const string btnText = "确  定";
        var btnWidth = 160;
        var btnHeight = 50;
        _confirmBounds = new Rectangle(
            centerX - btnWidth / 2,
            yPositionOnScreen + WindowHeight - 90,
            btnWidth, btnHeight);

        var btnColor = _confirmBounds.Contains(Game1.getMouseX(), Game1.getMouseY())
            ? Color.LightGoldenrodYellow
            : Color.Wheat;

        drawTextureBox(b, _confirmBounds.X, _confirmBounds.Y, _confirmBounds.Width, _confirmBounds.Height, btnColor);

        var btnTextSize = Game1.smallFont.MeasureString(btnText);
        b.DrawString(Game1.smallFont, btnText,
            new Vector2(
                _confirmBounds.X + (_confirmBounds.Width - btnTextSize.X) / 2f,
                _confirmBounds.Y + (_confirmBounds.Height - btnTextSize.Y) / 2f),
            Color.Black);
    }

    // ==========================================
    //  输入处理
    // ==========================================

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        switch (_state)
        {
            case UIState.ShowingQuestion:
                HandleQuestionClick(x, y);
                break;

            case UIState.ShowingFeedback:
                // 反馈期间点击无效，等待自动推进
                break;

            case UIState.ShowingResults:
                HandleResultsClick(x, y);
                break;
        }
    }

    private void HandleQuestionClick(int x, int y)
    {
        // 点击关闭按钮
        if (_closeBounds.Contains(x, y))
        {
            CloseWindow();
            return;
        }

        // 点击选项按钮
        for (var i = 0; i < _optionBounds.Count; i++)
        {
            if (!_optionBounds[i].Contains(x, y))
                continue;

            // 保存当前题目（SubmitAnswer 会推进 CurrentIndex，所以要先保存）
            _feedbackQuestion = _session.GetCurrentQuestion();
            _lastSelectedIndex = i;
            _lastAnswerCorrect = _session.SubmitAnswer(i);

            // 播放音效
            Game1.playSound(_lastAnswerCorrect ? "coin" : "stoneStep");

            // 切换到反馈状态
            _state = UIState.ShowingFeedback;
            _feedbackTimer = FeedbackDurationMs;
            return;
        }
    }

    private void HandleResultsClick(int x, int y)
    {
        // 点击确认按钮
        if (_confirmBounds.Contains(x, y))
        {
            CloseWindow();
        }
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        // 右键等效于点击关闭
        if (_state == UIState.ShowingQuestion)
        {
            CloseWindow();
        }
    }

    public override void receiveKeyPress(Keys key)
    {
        if (_state != UIState.ShowingQuestion)
            return;

        // 数字键 1/2/3 或字母键 A/B/C 快速选择
        var optionIndex = key switch
        {
            Keys.D1 or Keys.A or Keys.NumPad1 => 0,
            Keys.D2 or Keys.B or Keys.NumPad2 => 1,
            Keys.D3 or Keys.C or Keys.NumPad3 => 2,
            Keys.Escape => -1,
            _ => -2
        };

        if (optionIndex == -1)
        {
            CloseWindow();
        }
        else if (optionIndex >= 0)
        {
            // 模拟点击对应的选项按钮
            var bounds = _optionBounds[optionIndex];
            receiveLeftClick(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, true);
        }
    }

    // ==========================================
    //  帧更新
    // ==========================================

    public override void update(GameTime time)
    {
        base.update(time);

        if (_state == UIState.ShowingFeedback)
        {
            _feedbackTimer -= time.ElapsedGameTime.TotalMilliseconds;
            if (_feedbackTimer <= 0)
            {
                if (_session.IsFinished)
                {
                    // 全部答完 → 进入结果页
                    _state = UIState.ShowingResults;
                    Game1.playSound("whistle");
                }
                else
                {
                    // 还有下一题
                    _state = UIState.ShowingQuestion;
                }
            }
        }
    }

    public override void performHoverAction(int x, int y)
    {
        // 悬停效果由 DrawQuestionContent 中检查鼠标位置实现
        // 这里主要处理手柄支持（暂不实现）
    }

    // ==========================================
    //  窗口生命周期
    // ==========================================

    /// <summary>
    /// 关闭窗口并触发回调
    /// </summary>
    private void CloseWindow()
    {
        Game1.playSound("bigDeSelect");
        if (_session.IsFinished)
            _onComplete?.Invoke(_session);
        else
            _onClose?.Invoke();
        exitThisMenu();
    }

    /// <summary>
    /// 紧急关闭（被其他菜单覆盖或返回标题时）
    /// </summary>
    public override void emergencyShutDown()
    {
        _onClose?.Invoke();
        base.emergencyShutDown();
    }
}
