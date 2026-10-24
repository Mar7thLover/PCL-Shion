namespace PCL.Shion.Core.Export;

/// <summary>
/// 导出配置文件（<c>export_config.txt</c>）的解析结果。文件分为三段，段与段之间以 <see cref="Separator"/> 分隔：
/// 键值段（名称、版本、导出选项等）、导出内容段（规则）与追加内容段（额外的文件或文件夹）。
/// </summary>
public sealed class ExportConfig
{
    /// <summary>段与段之间的分隔行。</summary>
    public const string Separator = "==============================================================";

    public string Name { get; private init; } = "";
    public string Version { get; private init; } = "";
    public bool IncludeLauncher { get; private init; }
    public bool IncludeLauncherCustom { get; private init; } = true;
    public bool DontCheckHostedAssets { get; private init; }
    public bool ModrinthUploadMode { get; private init; }

    /// <summary>配置文件中指定的导出路径；为空时由用户在保存对话框中选择。</summary>
    public string? PackPath { get; private init; }

    /// <summary>导出内容段的规则行；为 null 时表示使用界面勾选的选项。</summary>
    public List<string>? RulesOverrides { get; private init; }

    /// <summary>追加内容段的行；为 null 时表示没有追加内容。</summary>
    public List<string>? ExtraFiles { get; private init; }

    /// <summary>解析配置文件内容。段数不足时缺省部分保持为默认值。</summary>
    public static ExportConfig Parse(string content)
    {
        var segments = content.Split(Separator);

        var ini = new Dictionary<string, string>();
        foreach (var lineRaw in segments[0].Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = lineRaw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith('='))
                continue;
            var index = line.IndexOf(':');
            if (index > 0)
                ini[line[..index]] = line[(index + 1)..];
        }

        return new ExportConfig
        {
            Name = ini.GetValueOrDefault("Name", ""),
            Version = ini.GetValueOrDefault("Version", ""),
            IncludeLauncher = ParseBool(ini, "IncludeLauncher", false),
            IncludeLauncherCustom = ParseBool(ini, "IncludeLauncherCustom", true),
            ModrinthUploadMode = ParseBool(ini, "ModrinthUploadMode", false),
            DontCheckHostedAssets = ParseBool(ini, "DontCheckHostedAssets", false),
            PackPath = ini.GetValueOrDefault("PackPath"),
            RulesOverrides = segments.Length > 1 ? SplitLines(segments[1]) : null,
            ExtraFiles = segments.Length > 2 ? SplitLines(segments[2]) : null
        };
    }

    private static bool ParseBool(Dictionary<string, string> ini, string key, bool defaultValue) =>
        ini.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : defaultValue;

    private static List<string> SplitLines(string segment) =>
        segment.Replace("\r", "\n").Replace("\n\n", "\n").Split('\n').ToList();
}
