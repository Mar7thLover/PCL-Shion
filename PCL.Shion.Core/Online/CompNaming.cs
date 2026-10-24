using System.Text;

namespace PCL.Shion.Core.Online;

/// <summary>
/// 资源文件的命名与存放位置。
/// </summary>
public static class CompNaming
{
    /// <summary>
    /// 资源种类在实例中对应的子文件夹，没有则为空字符串。
    /// </summary>
    public static string GetSubFolder(CompType type) => type switch
    {
        CompType.Mod => "mods",
        CompType.ResourcePack => "resourcepacks",
        CompType.Shader => "shaderpacks",
        CompType.World => "saves",
        _ => ""
    };

    /// <summary>
    /// 生成资源文件的保存名。若有中文名，按 <paramref name="nameFormat"/> 组合中文名与原文件名：
    /// 0 为【中文】原名，1 为 [中文] 原名，2 为 中文-原名，3 为 原名-中文，其他为原名。
    /// </summary>
    /// <param name="project">工程。</param>
    /// <param name="file">文件。</param>
    /// <param name="chineseName">中文名，为 null 或空时不使用。</param>
    /// <param name="nameFormat">命名格式。</param>
    public static string GetFileName(CompProject project, CompFile file, string? chineseName = null, int nameFormat = 0)
    {
        string fileName;
        if (string.IsNullOrEmpty(chineseName) || chineseName == project.RawName)
        {
            fileName = file.FileName;
        }
        else
        {
            var cleanName = SanitizeChineseName(chineseName);
            fileName = nameFormat switch
            {
                0 => $"【{cleanName}】{file.FileName}",
                1 => $"[{cleanName}] {file.FileName}",
                2 => $"{cleanName}-{file.FileName}",
                3 => $"{file.FileName}-{cleanName}",
                _ => file.FileName
            };
        }

        if (file.Type == CompType.Mod)
            fileName = fileName.Replace("~", "-");
        return SanitizeFileName(fileName);
    }

    /// <summary>
    /// 把文件名中的非法字符替换为全角字符或下划线，并去掉首尾空白。
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "download";

        var sanitized = new StringBuilder(fileName.Length);
        foreach (var c in fileName)
        {
            sanitized.Append(c switch
            {
                '\\' => '＼',
                '/' => '／',
                ':' => '：',
                '*' => '＊',
                '?' => '？',
                '"' => '＂',
                '<' => '＜',
                '>' => '＞',
                '|' => '｜',
                _ when char.IsControl(c) => '_',
                _ => c
            });
        }

        var result = sanitized.ToString().Trim();
        return result is "" or "." or ".." ? "download" : result;
    }

    private static string SanitizeChineseName(string name)
    {
        var first = name;
        var parenthesis = first.IndexOf(" (", StringComparison.Ordinal);
        if (parenthesis >= 0) first = first[..parenthesis];
        var dash = first.IndexOf(" - ", StringComparison.Ordinal);
        if (dash >= 0) first = first[..dash];
        return first.Replace(@"\", "＼")
            .Replace("/", "／").Replace("|", "｜").Replace(":", "：").Replace("<", "＜").Replace(">", "＞")
            .Replace("*", "＊").Replace("?", "？").Replace("\"", "").Replace("： ", "：");
    }
}
