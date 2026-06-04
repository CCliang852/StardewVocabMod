using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace StardewVocabMod.UI;

/// <summary>
/// 单条奖励信息
/// </summary>
public class RewardItem
{
    /// <summary>物品/奖励名称（中文）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>数量，0 表示不显示数量</summary>
    public int Quantity { get; set; }

    /// <summary>是否为特殊奖励（如星之果实碎屑）</summary>
    public bool IsSpecial { get; set; }

    public string DisplayText => Quantity > 0 ? $"{Name} ×{Quantity}" : Name;
}

/// <summary>
/// 奖励通知弹窗：显示答题后获得的奖励
/// </summary>
public class RewardNotification : IClickableMenu
{
    private const int WindowWidth = 420;
    private const int BaseHeight = 160;
    private const int ItemLineHeight = 30;

    private readonly List<RewardItem> _rewards;
    private readonly int _windowHeight;

    private Rectangle _okBounds;

    public RewardNotification(List<RewardItem> rewards)
        : base(0, 0, WindowWidth, 0)
    {
        _rewards = rewards;

        // 根据奖励条数动态计算高度
        _windowHeight = Math.Max(BaseHeight, 120 + rewards.Count * ItemLineHeight + 70);
        height = _windowHeight;

        var centerPos = StardewValley.Utility.getTopLeftPositionForCenteringOnScreen(WindowWidth, _windowHeight);
        xPositionOnScreen = (int)centerPos.X;
        yPositionOnScreen = (int)centerPos.Y;

        // OK 按钮
        _okBounds = new Rectangle(
            xPositionOnScreen + WindowWidth / 2 - 60,
            yPositionOnScreen + _windowHeight - 55,
            120, 40);

        Game1.playSound("reward");
    }

    /// <summary>
    /// 快捷方法：显示单条文字奖励
    /// </summary>
    public static void Show(string message)
    {
        var rewards = new List<RewardItem>
        {
            new() { Name = message, IsSpecial = true }
        };
        Game1.activeClickableMenu = new RewardNotification(rewards);
    }

    /// <summary>
    /// 快捷方法：显示物品列表奖励
    /// </summary>
    public static void Show(List<RewardItem> rewards)
    {
        if (rewards == null || rewards.Count == 0) return;
        Game1.activeClickableMenu = new RewardNotification(rewards);
    }

    public override void draw(SpriteBatch b)
    {
        // 半透明背景
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);

        // 主窗口
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, WindowWidth, _windowHeight, Color.White);

        // 标题
        const string title = "🎁 奖励获得！";
        var titleSize = Game1.dialogueFont.MeasureString(title);
        b.DrawString(Game1.dialogueFont, title,
            new Vector2(
                xPositionOnScreen + (WindowWidth - titleSize.X) / 2f,
                yPositionOnScreen + 25),
            Color.DarkGoldenrod);

        // 分隔线
        var lineY = yPositionOnScreen + 65;
        b.Draw(Game1.staminaRect,
            new Rectangle(xPositionOnScreen + 30, lineY, WindowWidth - 60, 2),
            Color.Goldenrod * 0.5f);

        // 奖励列表
        var itemStartY = lineY + 15;
        for (var i = 0; i < _rewards.Count; i++)
        {
            var reward = _rewards[i];
            var text = reward.DisplayText;
            var color = reward.IsSpecial ? Color.MediumPurple : Color.DarkGreen;

            var textSize = Game1.smallFont.MeasureString(text);
            b.DrawString(Game1.smallFont, text,
                new Vector2(
                    xPositionOnScreen + (WindowWidth - textSize.X) / 2f,
                    itemStartY + i * ItemLineHeight),
                color);
        }

        // OK 按钮
        var btnColor = _okBounds.Contains(Game1.getMouseX(), Game1.getMouseY())
            ? Color.LightGoldenrodYellow
            : Color.Wheat;

        drawTextureBox(b, _okBounds.X, _okBounds.Y, _okBounds.Width, _okBounds.Height, btnColor);

        const string okText = "收下！";
        var okSize = Game1.smallFont.MeasureString(okText);
        b.DrawString(Game1.smallFont, okText,
            new Vector2(
                _okBounds.X + (_okBounds.Width - okSize.X) / 2f,
                _okBounds.Y + (_okBounds.Height - okSize.Y) / 2f),
            Color.Black);

        drawMouse(b);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (_okBounds.Contains(x, y))
        {
            Close();
        }
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape || key == Keys.Enter)
        {
            Close();
        }
    }

    private void Close()
    {
        Game1.playSound("bigDeSelect");
        exitThisMenu();
    }

    public override void emergencyShutDown()
    {
        base.emergencyShutDown();
    }
}
