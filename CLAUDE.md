# Stardew Valley 背单词 Mod

## 项目简介
为星露谷增加英语单词学习玩法的 SMAPI Mod。支持固定时间学习、NPC 交互背词、公告栏挑战三种模式，面向中文玩家。

## 项目状态
🎉 **全部 7 个阶段已完成**（2026-06-01）

## 标准文件索引
- 需求文档: [docs/requirements.md](docs/requirements.md)
- 技术设计: [docs/technical-design.md](docs/technical-design.md)
- 编码规范: [docs/coding-standards.md](docs/coding-standards.md)
- 执行步骤: [docs/execution-steps.md](docs/execution-steps.md)
- 开发日志: [devlog/](devlog/)
- 用户手册: [README.md](README.md)

## 工作约定
1. 每次对话开始时，先阅读 `devlog/` 中最新的日志文件和 `docs/execution-steps.md`，了解当前进度
2. 严格遵循 `docs/coding-standards.md` 中的编码规范
3. 每完成一个小步骤，必须同步更新 `devlog/` 和 `docs/execution-steps.md`
4. 如果需求或设计有变更，同步更新 `docs/requirements.md` 或 `docs/technical-design.md`
5. 遇到不确定的需求，先查 `docs/requirements.md`，无解则问用户
6. 每完成一步向用户确认后再继续

## 技术速览
- **框架**: SMAPI 4.0+, .NET 6, C# 10
- **事件驱动**: 零 Harmony Patch，全部使用 SMAPI 事件
- **UI**: IClickableMenu 自定义窗口 + GMCM 配置面板
- **数据**: System.Text.Json + SMAPI Save Data API
- **词库**: 1037 个 CET-6 单词 + 985 条词关系
