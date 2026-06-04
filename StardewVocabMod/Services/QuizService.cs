using System;
using System.Collections.Generic;
using System.Linq;
using StardewVocabMod.Data;

namespace StardewVocabMod.Services;

/// <summary>
/// 单道答题的数据模型
/// </summary>
public class QuizQuestion
{
    /// <summary>单词词条</summary>
    public WordEntry Word { get; set; } = null!;

    /// <summary>3 个中文选项（已打乱顺序）</summary>
    public List<string> Options { get; set; } = new();

    /// <summary>正确选项在 Options 中的索引</summary>
    public int CorrectIndex { get; set; }
}

/// <summary>
/// 答题会话：管理题目列表、当前进度、判分
/// </summary>
public class QuizSession
{
    /// <summary>本次答题的所有题目</summary>
    public List<QuizQuestion> Questions { get; private set; } = new();

    /// <summary>当前题目索引（0-based）</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>已答对题数</summary>
    public int CorrectCount { get; private set; }

    /// <summary>答错的单词（用于后续复习）</summary>
    public List<string> WrongWords { get; private set; } = new();

    /// <summary>总题数</summary>
    public int TotalCount => Questions.Count;

    /// <summary>是否已答完全部题目</summary>
    public bool IsFinished => CurrentIndex >= TotalCount;

    /// <summary>正确率（0~1）</summary>
    public float Score => TotalCount > 0 ? (float)CorrectCount / TotalCount : 0f;

    /// <summary>
    /// 获取当前题目，如果已结束则返回 null
    /// </summary>
    public QuizQuestion? GetCurrentQuestion()
    {
        if (IsFinished || Questions.Count == 0)
            return null;
        return Questions[CurrentIndex];
    }

    /// <summary>
    /// 提交答案并自动前进到下一题
    /// </summary>
    /// <param name="selectedIndex">玩家选择的选项索引</param>
    /// <returns>true 表示答对</returns>
    public bool SubmitAnswer(int selectedIndex)
    {
        var question = GetCurrentQuestion();
        if (question == null)
            return false;

        var isCorrect = selectedIndex == question.CorrectIndex;
        if (isCorrect)
        {
            CorrectCount++;
        }
        else
        {
            WrongWords.Add(question.Word.Word);
        }

        CurrentIndex++;
        return isCorrect;
    }
}

/// <summary>
/// 出题服务：根据不同的学习模式生成答题会话
/// </summary>
public class QuizService
{
    private readonly WordBank _wordBank;

    public QuizService(WordBank wordBank)
    {
        _wordBank = wordBank;
    }

    /// <summary>
    /// 从词库随机出题（固定学习模式用）
    /// </summary>
    /// <param name="count">题目数量</param>
    public QuizSession GenerateRandomQuiz(int count)
    {
        var words = _wordBank.GetRandomWords(count);
        return CreateSession(words);
    }

    /// <summary>
    /// 从错词列表出复习题（NPC 交互模式用），错词不够则随机补充
    /// </summary>
    /// <param name="wrongWords">玩家错词列表</param>
    /// <param name="count">目标题数</param>
    public QuizSession GenerateReviewQuiz(List<string> wrongWords, int count)
    {
        var selected = new List<WordEntry>();

        // 先从词库查找错词对应的词条
        foreach (var w in wrongWords)
        {
            var entry = _wordBank.FindWord(w);
            if (entry != null)
                selected.Add(entry);
        }

        // 错词不够时随机补充
        if (selected.Count < count)
        {
            var extra = _wordBank.GetRandomWords(count - selected.Count);
            foreach (var e in extra)
            {
                // 避免重复
                if (!selected.Any(s => string.Equals(s.Word, e.Word, StringComparison.OrdinalIgnoreCase)))
                    selected.Add(e);
            }
        }
        else
        {
            // 错词太多则取错误次数最多的一部分（由调用方排序后传入）
            selected = selected.Take(count).ToList();
        }

        return CreateSession(selected);
    }

    /// <summary>
    /// 从指定词根的单词出题（挑战模式用）
    /// </summary>
    /// <param name="prefix">词根前缀，如 "con"</param>
    /// <param name="count">题目数量</param>
    public QuizSession GenerateChallengeQuiz(string prefix, int count)
    {
        var words = _wordBank.GetWordsByRootPrefix(prefix, count);
        if (words.Count == 0)
            return new QuizSession();

        // 随机取 count 个
        if (words.Count > count)
            words = words.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();

        return CreateSession(words);
    }

    /// <summary>
    /// 根据词条列表生成题目（为每道题生成 3 个中文选项）
    /// </summary>
    private QuizSession CreateSession(List<WordEntry> words)
    {
        var session = new QuizSession();

        foreach (var word in words)
        {
            var options = _wordBank.GenerateOptions(word.Word, 3);

            // 小词库边界：如果选项不足 3 个，用占位符补足
            while (options.Count < 3)
            {
                options.Add($"[选项{options.Count + 1}]");
            }

            // 在已打乱的选项中定位正确释义的位置
            var correctIndex = options.IndexOf(word.Chinese);
            if (correctIndex < 0)
            {
                // 极其罕见：正确释义不在选项中（例如释义同其他词完全一致）
                // 这时把第一个选项替换为正确释义
                options[0] = word.Chinese;
                correctIndex = 0;
            }

            session.Questions.Add(new QuizQuestion
            {
                Word = word,
                Options = options,
                CorrectIndex = correctIndex
            });
        }

        return session;
    }
}
