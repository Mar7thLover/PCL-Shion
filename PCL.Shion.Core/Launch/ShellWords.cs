using System.Text;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Launch;

/// <summary>
/// 把用户填写的一行命令或参数拆分成单个参数，不做变量展开。未闭合的引号视为在末尾闭合。
/// </summary>
internal static class ShellWords
{
    /// <summary>
    /// Windows 上按命令行参数的解析规则拆分（反斜杠只在引号前转义，路径可以照常书写），其他系统按 POSIX shell 规则拆分。
    /// </summary>
    public static IReadOnlyList<string> Split(string text)
    {
        return PlatformInfo.Os == OsKind.Windows ? SplitWindows(text) : SplitPosix(text);
    }

    /// <summary>
    /// POSIX shell 规则：空白分隔；单引号内全部按字面；双引号内反斜杠只转义 " \ $ `；引号外反斜杠转义下一个字符。
    /// </summary>
    public static IReadOnlyList<string> SplitPosix(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        var inWord = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                if (inWord)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    inWord = false;
                }
                continue;
            }

            inWord = true;
            switch (c)
            {
                case '\'':
                    var end = text.IndexOf('\'', i + 1);
                    if (end < 0)
                        end = text.Length;
                    word.Append(text, i + 1, end - i - 1);
                    i = end;
                    break;
                case '"':
                    for (i++; i < text.Length && text[i] != '"'; i++)
                    {
                        if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] is '"' or '\\' or '$' or '`')
                            i++;
                        word.Append(text[i]);
                    }
                    break;
                case '\\':
                    if (i + 1 < text.Length)
                        word.Append(text[++i]);
                    break;
                default:
                    word.Append(c);
                    break;
            }
        }

        if (inWord)
            words.Add(word.ToString());
        return words;
    }

    /// <summary>
    /// Windows 命令行规则：引号外的空白分隔；双引号切换引用状态；
    /// 引号前的 2n 个反斜杠变为 n 个且引号照常切换，2n+1 个变为 n 个加一个字面引号；其余反斜杠按字面。
    /// </summary>
    public static IReadOnlyList<string> SplitWindows(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        var inWord = false;
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c) && !quoted)
            {
                if (inWord)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    inWord = false;
                }
                continue;
            }

            inWord = true;
            if (c == '\\')
            {
                var start = i;
                while (i < text.Length && text[i] == '\\')
                    i++;
                var count = i - start;
                if (i < text.Length && text[i] == '"')
                {
                    word.Append('\\', count / 2);
                    if (count % 2 == 1)
                        word.Append('"');
                    else
                        quoted = !quoted;
                }
                else
                {
                    word.Append('\\', count);
                    i--;
                }
            }
            else if (c == '"')
            {
                quoted = !quoted;
            }
            else
            {
                word.Append(c);
            }
        }

        if (inWord)
            words.Add(word.ToString());
        return words;
    }
}
