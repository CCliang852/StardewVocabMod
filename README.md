# 星露谷背单词 (StardewVocabMod)

[![SMAPI](https://img.shields.io/badge/SMAPI-4.0+-blue)](https://smapi.io/)
[![Stardew Valley](https://img.shields.io/badge/Stardew%20Valley-1.6+-green)](https://www.stardewvalley.net/)
[![License](https://img.shields.io/badge/license-MIT-brightgreen)](LICENSE)

在《星露谷物语》中轻松学习 CET-6 英语单词！寓教于乐，三种模式让你的农场生活更有收获。

## ✨ 功能特色

- **📖 固定学习模式**：每日定时 + 睡前触发答题，资源+种子混合奖励
- **💬 NPC 互动模式**：对话后复习错词，答对加好感，村民个性化台词
- **🏆 词根挑战模式**：同词根家族词辨析，完成赢取优质洒水器
- **🎛️ GMCM 配置面板**：8 项可调参数，游戏内即时修改
- **📊 学习进度追踪**：掌握/错词/未接触一目了然
- **📝 自定义词库**：支持放入自定义 JSON/TXT 词库文件
- **🛡️ 零 Harmony Patch**：全部使用 SMAPI 内置事件，兼容性极佳

## 📦 安装方法

1. 安装 [SMAPI 4.0+](https://smapi.io/)
2. 下载本 Mod 的最新 [Release](https://github.com/CCliang852/StardewVocabMod/releases) 压缩包
3. 解压到 `Stardew Valley/Mods/` 文件夹
4. （推荐）安装 [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) 以在游戏内调整配置

```
Stardew Valley/Mods/
└── StardewVocabMod/
    ├── StardewVocabMod.dll
    ├── manifest.json
    └── Assets/
        ├── cet6_core.json
        ├── word_relations.json
        └── npc_lines.json
```

## 🎮 使用指南

### 固定学习模式
无需操作，每天到配置时间（默认早上 7:00）和睡前自动弹出答题窗口。战斗中会自动延迟 1 小时。

### NPC 交互模式
与村民对话（当天第二次起触发），答题复习错词。答对一题 +1 好感，答错 NPC 会吐槽你～

### 挑战模式
在 SMAPI 控制台（黑窗口）输入命令：

| 命令 | 说明 |
|------|------|
| `vc accept` | 接受词根挑战 |
| `vc status` | 查看挑战状态 |
| `vp` | 查看学习进度统计 |

## 🎛️ 配置项

安装 GMCM 后可在 设置 → Mod Options 中调整，或直接编辑 `config.json`：

| 配置项 | 默认值 | 范围 | 说明 |
|--------|--------|------|------|
| `ModEnabled` | `true` | — | Mod 总开关 |
| `FixedQuizTime` | `700` | 600-2600 | 每日触发时间（如 700=7:00） |
| `FixedQuizWordCount` | `20` | 3-100 | 每次题数 |
| `PassThreshold` | `0.8` | 0.0-1.0 | 通过阈值 |
| `NpcDailyLimit` | `3` | 1-10 | 每村民每日上限 |
| `ChallengeWordCount` | `15` | 3-30 | 挑战题数 |
| `ChallengeTimeLimitDays` | `3` | 1-14 | 挑战时限 |
| `ChallengeCooldownDays` | `7` | 1-28 | 挑战冷却 |

## 📝 自定义词库

将以下文件放入 Mod 文件夹即可自动导入：

**JSON 格式** (`custom_words.json`)：
```json
[
  { "Word": "example", "Chinese": "例子", "Difficulty": "custom" }
]
```

**TXT 格式** (任意 `.txt` 文件，每行 `英文 中文`)：
```
example 例子
vocabulary 词汇
```

## 🔧 开发

```bash
git clone https://github.com/CCliang852/StardewVocabMod.git
# 用 Visual Studio 或 Rider 打开 StardewVocabMod.sln
dotnet build
```

内置 1037 个 CET-6 高频词汇 + 985 条词关系表（近义/形近/同词根）。

## 📄 许可

MIT License — 随意使用、修改和分享。
