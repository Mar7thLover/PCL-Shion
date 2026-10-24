using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace PCL.Shion.Localization;

/// <summary>
/// 读取嵌入的语言文件。核心语言文件取自 PCL.Core，嵌入名为 <c>PCL.Shion.Lang.Core.&lt;语言&gt;.xaml</c>；
/// 各页面自己的补充文件嵌入名为 <c>PCL.Shion.Lang.&lt;名称&gt;.&lt;语言&gt;.xaml</c>，格式与核心文件相同。
/// </summary>
internal static class LangResources
{
    private const string Prefix = "PCL.Shion.Lang.";
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    /// 按语言链从前到后读入，后读入的覆盖先读入的。同一语言内核心文件最先读入，其余按名称顺序。
    /// </summary>
    public static Dictionary<string, string> Load(IEnumerable<string> languageChain)
    {
        var assembly = typeof(LangResources).Assembly;
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var code in languageChain)
        {
            foreach (var name in Find(assembly, Prefix, ".xaml", code, $"{Prefix}Core.{code}.xaml"))
                Read(assembly, name, table);
        }

        return table;
    }

    /// <summary>
    /// 找出名称形如 <c>前缀*.语言代码.扩展名</c> 的嵌入资源，<paramref name="firstName"/> 排在最前，其余按名称排序。
    /// </summary>
    public static IEnumerable<string> Find(Assembly assembly, string prefix, string extension, string languageCode, string firstName)
    {
        var suffix = $".{languageCode}{extension}";
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal)
                           && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => !string.Equals(name, firstName, StringComparison.OrdinalIgnoreCase))
            .ThenBy(name => name, StringComparer.Ordinal);
    }

    private static void Read(Assembly assembly, string resourceName, Dictionary<string, string> table)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreComments = true };
        try
        {
            using var reader = XmlReader.Create(stream, settings);
            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element
                    && reader.LocalName == "String"
                    && reader.GetAttribute("Key", XamlNamespace) is { } key)
                    table[key] = reader.ReadElementContentAsString();
                else
                    reader.Read();
            }
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException($"语言文件 {resourceName} 无法解析：{ex.Message}", ex);
        }
    }
}
