using System;
using System.Collections.Generic;
using StardewValley;
using StardewVocabMod.UI;

namespace StardewVocabMod.Services;

/// <summary>
/// 奖励服务：根据答题成绩发放物品奖励
/// </summary>
public class RewardService
{
    private static readonly Random _random = new();

    /// <summary>通过阈值（正确率 ≥ 此值才算通过），默认值，可被配置覆盖</summary>
    public const float DefaultPassThreshold = 0.8f;

    /// <summary>额外奖励概率</summary>
    public const double ExtraRewardChance = 0.10;

    // ===== 奖励池 =====

    /// <summary>基础资源奖励池（物品ID, 名称, 最小数量, 最大数量, 权重）</summary>
    private static readonly (int Id, string Name, int Min, int Max, int Weight)[] ResourcePool =
    {
        (388, "木材",       5, 20,  10),
        (390, "石头",       5, 20,  10),
        (771, "纤维",       3, 10,  8),
        (382, "煤炭",       1, 5,   6),
        (378, "铜矿石",     1, 5,   6),
        (380, "铁矿石",     1, 3,   4),
        (709, "硬木",       1, 3,   3),
        (384, "金矿石",     1, 2,   2),
        (338, "精炼石英",   1, 2,   2),
    };

    /// <summary>种子/食材奖励池</summary>
    private static readonly (int Id, string Name, int Min, int Max, int Weight)[] SeedPool =
    {
        (770, "混合种子",   2, 5,   10),
        (472, "防风草种子", 2, 5,   8),
        (475, "土豆种子",   2, 4,   8),
        (474, "花椰菜种子", 2, 4,   8),
        (473, "青豆种子",   2, 4,   7),
        (745, "草莓种子",   1, 3,   5),
        (481, "蓝莓种子",   1, 3,   5),
    };

    /// <summary>额外特殊奖励池（10% 概率触发）</summary>
    private static readonly (int Id, string Name, int Qty, bool IsSpecial, int Weight)[] ExtraPool =
    {
        (466, "高级肥料",   3, false, 4),
        (465, "生长激素",   3, false, 3),
        (349, "能量剂",     1, false, 2),
        (434, "星之果实",   1, true,  1),  // 最稀有
    };

    /// <summary>
    /// 发放奖励
    /// </summary>
    /// <param name="score">正确率（0~1）</param>
    /// <param name="totalCount">总题数</param>
    /// <param name="correctCount">答对题数</param>
    /// <returns>奖励物品列表（用于显示通知）</returns>
    public static List<RewardItem> GrantRewards(float score, int totalCount, int correctCount,
        float passThreshold = DefaultPassThreshold)
    {
        var rewards = new List<RewardItem>();

        if (score < passThreshold)
        {
            // 未通过，给少量鼓励奖
            GrantConsolation(rewards);
            ShowNotification(rewards);
            return rewards;
        }

        // === 通过奖励 ===

        // 1. 从资源池随机选 2~3 种
        var resourceCount = _random.Next(2, 4);
        var pickedResources = PickWeighted(ResourcePool, resourceCount);
        foreach (var (id, name, min, max, _) in pickedResources)
        {
            var qty = _random.Next(min, max + 1);
            AddItemToInventory(id, qty);
            rewards.Add(new RewardItem { Name = name, Quantity = qty });
        }

        // 2. 从种子池随机选 1~2 种
        var seedCount = _random.Next(1, 3);
        var pickedSeeds = PickWeighted(SeedPool, seedCount);
        foreach (var (id, name, min, max, _) in pickedSeeds)
        {
            var qty = _random.Next(min, max + 1);
            AddItemToInventory(id, qty);
            rewards.Add(new RewardItem { Name = name, Quantity = qty });
        }

        // 3. 10% 概率额外奖励
        if (_random.NextDouble() < ExtraRewardChance)
        {
            var extra = PickExtraReward();
            if (extra != null)
            {
                var (id, name, qty, isSpecial) = extra.Value;
                AddItemToInventory(id, qty);
                rewards.Add(new RewardItem
                {
                    Name = name,
                    Quantity = qty,
                    IsSpecial = isSpecial
                });
            }
        }

        ShowNotification(rewards);
        return rewards;
    }

    /// <summary>
    /// 未通过时的安慰奖
    /// </summary>
    private static void GrantConsolation(List<RewardItem> rewards)
    {
        // 给几块石头或木材作为参与奖励
        var roll = _random.Next(3);
        switch (roll)
        {
            case 0:
                AddItemToInventory(388, 3); // 木材 x3
                rewards.Add(new RewardItem { Name = "木材", Quantity = 3 });
                break;
            case 1:
                AddItemToInventory(390, 3); // 石头 x3
                rewards.Add(new RewardItem { Name = "石头", Quantity = 3 });
                break;
            default:
                AddItemToInventory(770, 1); // 混合种子 x1
                rewards.Add(new RewardItem { Name = "混合种子", Quantity = 1 });
                break;
        }
    }

    /// <summary>
    /// 按权重随机选取指定数量的物品（不重复）
    /// </summary>
    private static List<(int Id, string Name, int Min, int Max, int Weight)> PickWeighted(
        (int Id, string Name, int Min, int Max, int Weight)[] pool, int count)
    {
        var result = new List<(int Id, string Name, int Min, int Max, int Weight)>();
        var available = new List<(int Id, string Name, int Min, int Max, int Weight)>(pool);

        for (var i = 0; i < count && available.Count > 0; i++)
        {
            var totalWeight = 0;
            foreach (var item in available) totalWeight += item.Weight;

            var roll = _random.Next(totalWeight);
            var cumulative = 0;
            var pickedIdx = 0;

            for (var j = 0; j < available.Count; j++)
            {
                cumulative += available[j].Weight;
                if (roll < cumulative)
                {
                    pickedIdx = j;
                    break;
                }
            }

            result.Add(available[pickedIdx]);
            available.RemoveAt(pickedIdx);
        }

        return result;
    }

    /// <summary>
    /// 向玩家背包添加物品，背包满则丢在地上
    /// </summary>
    private static void AddItemToInventory(int itemId, int quantity)
    {
        // Stardew Valley 1.6 使用字符串 ID 构造 Object
        var item = new StardewValley.Object(itemId.ToString(), quantity);
        if (Game1.player.addItemToInventoryBool(item))
        {
            // 成功加入背包
        }
        else
        {
            // 背包满，丢在玩家脚下
            Game1.createItemDebris(item, Game1.player.getStandingPosition(), -1);
        }
    }

    /// <summary>
    /// 从额外奖励池按权重随机选取一件
    /// </summary>
    private static (int Id, string Name, int Qty, bool IsSpecial)? PickExtraReward()
    {
        var totalWeight = 0;
        foreach (var item in ExtraPool) totalWeight += item.Weight;

        var roll = _random.Next(totalWeight);
        var cumulative = 0;

        foreach (var item in ExtraPool)
        {
            cumulative += item.Weight;
            if (roll < cumulative)
                return (item.Id, item.Name, item.Qty, item.IsSpecial);
        }

        return null;
    }

    /// <summary>
    /// 显示奖励通知
    /// </summary>
    private static void ShowNotification(List<RewardItem> rewards)
    {
        if (rewards.Count > 0)
        {
            RewardNotification.Show(rewards);
        }
    }

    /// <summary>
    /// 判断是否通过答题
    /// </summary>
    public static bool IsPassed(float score, float passThreshold = DefaultPassThreshold) => score >= passThreshold;
}
