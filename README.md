# 星露谷背单词 (StardewVocabMod)

在《星露谷物语》中学习 CET-6 英语单词！三种游戏模式，寓教于乐。

## 📦 安装

1. 安装 [SMAPI 4.0+](https://smapi.io/)
2. 下载本 Mod 的 zip 包，解压到 `Stardew Valley/Mods/` 文件夹
3. （可选）安装 [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) 以在游戏内修改配置

## 🎮 三种学习模式

### 📖 固定学习模式
- **触发**：每日固定时间 + 睡前结算（时间可配置，默认 7:00）
- **题量**：每次 20 题（可配置）
- **奖励**：正确率 ≥ 80% 可获得资源+种子混合奖励，10% 概率额外特殊奖励
- **延迟**：战斗/钓鱼/菜单中自动推迟 1 小时，左下角提醒

### 💬 NPC 交互模式
- **触发**：与村民对话结束后（每日首次闲聊跳过）
- **题量**：每次 5 题，优先复习错词
- **好感**：每答对一题 +1 该村民好感度
- **台词**：答对 NPC 夸奖，答错 NPC 阴阳怪气（34 位村民个性化台词）
- **限制**：每个村民每天最多 3 次（可配置）

### 🏆 挑战模式
- **入口**：SMAPI 控制台输入 `vocab-challenge accept`（或简写 `vc accept`）
- **规则**：15 道同词根单词题（如 con-/pro-/com- 开头），3 天内完成
- **奖励**：≥ 80% 正确率获得 3 个优质洒水器
- **冷却**：完成/失败后 7 天冷却（可配置）

## 🎛️ 配置

安装 GMCM 后可在游戏设置中调整，或直接编辑 `config.json`：

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| `ModEnabled` | `true` | Mod 总开关 |
| `FixedQuizTime` | `700` | 固定模式触发时间 (6:00-26:00, 整百) |
| `FixedQuizWordCount` | `20` | 固定模式每次题数 (3-100) |
| `PassThreshold` | `0.8` | 通过阈值 (0.0-1.0) |
| `NpcDailyLimit` | `3` | 每村民每日上限 (1-10) |
| `ChallengeWordCount` | `15` | 挑战模式题数 (3-30) |
| `ChallengeTimeLimitDays` | `3` | 挑战时限 (1-14) |
| `ChallengeCooldownDays` | `7` | 挑战冷却天数 (1-28) |

## 📝 自定义词库

将以下文件放入 Mod 文件夹即可自动导入：

- **JSON 格式**：`custom_words.json`，格式同 `Assets/cet6_core.json`
  ```json
  [
    { "Word": "example", "Chinese": "例子", "Difficulty": "custom" }
  ]
  ```
- **TXT 格式**：任意 `.txt` 文件，每行 `英文 中文释义`（空格/Tab 分隔）
  ```
  example 例子
  vocabulary 词汇
  ```

## 🛠️ 控制台命令

| 命令 | 说明 |
|------|------|
| `vocab-progress` / `vp` | 查看学习进度（已掌握/错词/未接触） |
| `vocab-challenge status` | 查看挑战状态 |
| `vocab-challenge accept` | 接受词根挑战 |
| `vc status` / `vc accept` | 同上（简写） |

## 📂 项目结构

```
StardewVocabMod/
├── ModEntry.cs              # SMAPI 入口
├── Config/ModConfig.cs      # 配置模型
├── Data/
│   ├── WordBank.cs          # 词库加载/查询
│   ├── WordEntry.cs         # 单词数据模型
│   ├── WordRelation.cs      # 近义/形近关系模型
│   └── PlayerProgress.cs    # 玩家进度存档
├── Modes/
│   ├── FixedMode.cs         # 固定学习模式
│   ├── NpcMode.cs           # NPC 交互模式
│   └── ChallengeMode.cs     # 挑战模式
├── Services/
│   ├── QuizService.cs       # 出题/判分
│   ├── RewardService.cs     # 奖励发放
│   └── ScheduleService.cs   # 延迟调度
├── UI/
│   ├── VocabQuizWindow.cs   # 答题窗口
│   ├── RewardNotification.cs # 奖励弹窗
│   └── ModConfigMenu.cs     # GMCM 集成
└── Assets/
    ├── cet6_core.json       # CET-6 词库 (1037 词)
    ├── word_relations.json  # 词关系表
    └── npc_lines.json       # NPC 台词
```

## ⚙️ 兼容性

- **SMAPI**: 4.0+
- **Stardew Valley**: 1.6+
- **.NET**: 6.0
- **语言**: 中文界面

## 📄 许可

MIT License
