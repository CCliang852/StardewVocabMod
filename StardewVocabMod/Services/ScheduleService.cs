using System;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;

namespace StardewVocabMod.Services;

/// <summary>
/// 延迟调度服务：管理答题触发时机、检测玩家忙碌状态、处理延迟重试
/// </summary>
public class ScheduleService
{
    private readonly IMonitor _monitor;

    /// <summary>今日下午答题是否已触发过</summary>
    public bool AfternoonQuizDone { get; set; }

    /// <summary>今日睡前答题是否已触发过</summary>
    public bool SleepQuizDone { get; set; }

    /// <summary>是否有待处理的延迟答题（战斗/钓鱼结束后重试）</summary>
    public bool HasPendingQuiz { get; private set; }

    /// <summary>延迟答题的重试时间（游戏时间，如 1500）</summary>
    public int PendingQuizTime { get; private set; }

    /// <summary>玩家最后一次忙碌状态的游戏时间，用于防重复提醒</summary>
    private int _lastBusyWarningTime = -1;

    public ScheduleService(IMonitor monitor)
    {
        _monitor = monitor;
    }

    /// <summary>
    /// 重置每日状态（新的一天开始时调用）
    /// </summary>
    public void ResetDaily()
    {
        AfternoonQuizDone = false;
        SleepQuizDone = false;
        HasPendingQuiz = false;
        PendingQuizTime = 0;
        _lastBusyWarningTime = -1;
    }

    /// <summary>
    /// 检查玩家当前是否忙碌（菜单打开中、战斗中或钓鱼中）
    /// </summary>
    public bool IsPlayerBusy()
    {
        // 检查是否有菜单打开（背包、箱子、商店、钓鱼等），都延迟
        if (Game1.activeClickableMenu != null)
            return true;

        // 检查附近是否有怪物
        var player = Game1.player;
        var location = player?.currentLocation;
        if (location != null && player != null)
        {
            var playerPos = player.Position;
            const float dangerRange = 500f;

            foreach (var character in location.characters)
            {
                if (character is Monster monster
                    && !monster.IsInvisible
                    && monster.Health > 0
                    && Vector2Distance(playerPos, monster.Position) < dangerRange)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 尝试在指定时间触发答题。如果玩家忙碌则延迟 1 小时
    /// </summary>
    /// <param name="currentTime">当前游戏时间（如 1400）</param>
    /// <returns>true 表示现在可以触发，false 表示已延迟</returns>
    public bool TryTriggerAtTime(int currentTime)
    {
        if (AfternoonQuizDone)
            return false;

        if (IsPlayerBusy())
        {
            // 延迟到下一小时
            var delayedTime = currentTime + 100;
            if (delayedTime >= 2600)
                delayedTime = 600; // 跨天，但不会发生（14:00 延迟到 15:00）

            ScheduleDelayed(delayedTime);

            // 左下角提醒（每小时只提醒一次）
            if (currentTime != _lastBusyWarningTime)
            {
                Game1.addHUDMessage(new HUDMessage(
                    "检测到你在忙，背单词推迟到下一小时～", HUDMessage.newQuest_type));
                _lastBusyWarningTime = currentTime;
            }

            return false;
        }

        // 不忙，可以触发
        AfternoonQuizDone = true;
        HasPendingQuiz = false;
        return true;
    }

    /// <summary>
    /// 尝试触发延迟的答题
    /// </summary>
    /// <param name="currentTime">当前游戏时间</param>
    /// <returns>true 表示现在触发</returns>
    public bool TryTriggerPending(int currentTime)
    {
        if (!HasPendingQuiz)
            return false;

        if (currentTime >= PendingQuizTime)
        {
            return TryTriggerAtTime(currentTime);
        }

        return false;
    }

    /// <summary>
    /// 安排一个延迟答题
    /// </summary>
    private void ScheduleDelayed(int time)
    {
        HasPendingQuiz = true;
        PendingQuizTime = time;
        _monitor.Log($"答题已推迟到 {time / 100:D2}:{time % 100:D2}", LogLevel.Info);
    }

    /// <summary>
    /// 标记今日所有答题已完成（关窗时调用）
    /// </summary>
    public void MarkAllDone()
    {
        AfternoonQuizDone = true;
        SleepQuizDone = true;
        HasPendingQuiz = false;
    }

    private static float Vector2Distance(Microsoft.Xna.Framework.Vector2 a, Microsoft.Xna.Framework.Vector2 b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
}
