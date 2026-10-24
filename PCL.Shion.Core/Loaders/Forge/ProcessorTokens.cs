using System.Text;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Loaders.Forge;

/// <summary>
/// 处理器的变量与参数替换，规则与 Forge 安装器一致。
/// </summary>
internal static class ProcessorTokens
{
    /// <summary>
    /// 由 install_profile.json 的 data 生成变量表。取值有三种形式：<c>[Maven 坐标]</c> 为 libraries 下的文件路径，
    /// <c>'文字'</c> 为引号内的文字，其余为安装器中的文件路径，由 <paramref name="extract"/> 解压后换成解压出的文件路径。
    /// </summary>
    /// <param name="extract">把安装器中的条目解压到临时目录，返回解压出的文件路径。</param>
    public static Dictionary<string, string> CreateVariables(
        IReadOnlyDictionary<string, string> data,
        string librariesDirectory,
        Func<string, string> extract)
    {
        var variables = new Dictionary<string, string>();
        foreach (var (key, value) in data)
        {
            variables[key] = value switch
            {
                ['[', .., ']'] => GetLibraryPath(librariesDirectory, value[1..^1]),
                ['\'', .., '\''] => value[1..^1],
                _ => extract(value)
            };
        }
        return variables;
    }

    /// <summary>
    /// 整个参数为 <c>[Maven 坐标]</c> 时换成 libraries 下的文件路径，否则按 <see cref="Replace"/> 替换其中的变量。
    /// </summary>
    /// <exception cref="InvalidDataException">模板格式错误，或引用了未定义的变量。</exception>
    public static string ResolveArgument(string argument, string librariesDirectory, IReadOnlyDictionary<string, string> variables)
    {
        return argument is ['[', .., ']']
            ? GetLibraryPath(librariesDirectory, argument[1..^1])
            : Replace(argument, variables);
    }

    /// <summary>
    /// 替换模板中的变量：<c>{名称}</c> 换成变量的值，<c>'文字'</c> 原样保留引号内的文字，<c>\</c> 让下一个字符失去特殊含义。
    /// </summary>
    /// <exception cref="InvalidDataException">模板格式错误，或引用了未定义的变量。</exception>
    public static string Replace(string template, IReadOnlyDictionary<string, string> variables)
    {
        var result = new StringBuilder(template.Length);
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (c == '\\')
            {
                result.Append(ReadEscaped(template, ref i));
                continue;
            }
            if (c is not ('{' or '\''))
            {
                result.Append(c);
                continue;
            }

            var close = c == '{' ? '}' : '\'';
            var token = new StringBuilder();
            for (i++; ; i++)
            {
                if (i == template.Length)
                    throw new InvalidDataException($"Unclosed {c} in the processor template: {template}");
                if (template[i] == close)
                    break;
                token.Append(template[i] == '\\' ? ReadEscaped(template, ref i) : template[i]);
            }

            if (c == '\'')
                result.Append(token);
            else if (variables.TryGetValue(token.ToString(), out var value))
                result.Append(value);
            else
                throw new InvalidDataException($"Unknown variable {{{token}}} in the processor template: {template}");
        }
        return result.ToString();
    }

    /// <summary>
    /// Maven 坐标（可带 classifier 与 @扩展名）对应的库文件完整路径。
    /// </summary>
    public static string GetLibraryPath(string librariesDirectory, string mavenName)
    {
        return Path.Combine(librariesDirectory, MavenName.GetPath(mavenName).Replace('/', Path.DirectorySeparatorChar));
    }

    // index 指向反斜杠，读出它转义的字符并让 index 指向该字符
    private static char ReadEscaped(string template, ref int index)
    {
        if (++index == template.Length)
            throw new InvalidDataException($"Dangling escape in the processor template: {template}");
        return template[index];
    }
}
