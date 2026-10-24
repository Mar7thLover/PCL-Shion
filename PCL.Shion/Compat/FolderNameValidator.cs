using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;

namespace PCL.Shion;

/// <summary>
///     文件夹名称的校验规则，与 WPF 版 PCL.Core.Utils.Validate.FolderNameValidator 相同。
///     可用于 <see cref="Controls.MyTextBox.ValidateRules" />。
/// </summary>
public class FolderNameValidator : AbstractValidator<string>
{
    private static readonly Regex Ntfs83FileName = new(@".{2,}~\d", RegexOptions.Compiled);

    private static readonly string[] ReservedWords =
    [
        "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7",
        "COM8", "COM9", "COM¹", "COM²", "COM³", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7",
        "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³"
    ];

    /// <param name="parentFolder">所在的父文件夹，用于检查是否与现有文件夹重名。为空则不检查。</param>
    /// <param name="useMinecraftCharCheck">是否额外禁止 Minecraft 不允许的字符（<c>!</c> 与 <c>;</c>）。</param>
    /// <param name="ignoreCase">与现有文件夹比较重名时是否忽略大小写。</param>
    /// <param name="ignoreSameNameInParentFolder">是否跳过与现有文件夹重名的检查。</param>
    public FolderNameValidator(string? parentFolder = null, bool useMinecraftCharCheck = true, bool ignoreCase = true,
        bool ignoreSameNameInParentFolder = false)
    {
        RuleFor(x => x)
            .Cascade(CascadeMode.Stop)
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("输入内容不能为空！")
            .Must(x => !x.StartsWith(' ')).WithMessage("文件名不能以空格开头！")
            .Must(x => !x.EndsWith(' ')).WithMessage("文件名不能以空格结尾！")
            .Must(x => !x.EndsWith('.')).WithMessage("文件名不能以小数点结尾！")
            .Custom((fileName, context) =>
            {
                var invalid = FindInvalidStrings(fileName,
                    useMinecraftCharCheck ? new[] { "!;" } : Array.Empty<string>());
                if (invalid is not null)
                    context.AddFailure($"文件名不可包含 {invalid} 字符！");
            })
            .Custom((fileName, context) =>
            {
                var reserved = FindReservedWord(fileName);
                if (reserved is not null)
                    context.AddFailure($"文件名不可为 {reserved}！");
            })
            .Must(x => !Ntfs83FileName.IsMatch(x)).WithMessage("文件名不能包含这一特殊格式！")
            .Must(x => IsNotExistingFolder(parentFolder, x, ignoreCase, ignoreSameNameInParentFolder))
            .WithMessage("不可与现有文件夹重名！");
    }

    /// <summary>
    ///     找出名称中包含的非法字符（系统文件名非法字符加上额外的字符串），没有则返回 null。
    /// </summary>
    private static string? FindInvalidStrings(string input, string[] extraInvalidStrings)
    {
        if (string.IsNullOrEmpty(input))
            return null;

        var found = Path.GetInvalidFileNameChars()
            .Select(c => c.ToString())
            .Concat(extraInvalidStrings)
            .Where(s => input.Contains(s, StringComparison.Ordinal))
            .Distinct()
            .ToArray();
        return found.Length != 0 ? string.Join("  ", found) : null;
    }

    /// <summary>
    ///     名称（去掉扩展名后）是否为 Windows 保留名，是则返回该保留名。
    /// </summary>
    private static string? FindReservedWord(string input)
    {
        if (string.IsNullOrEmpty(input))
            return null;

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(input);
        return ReservedWords.FirstOrDefault(r => r.Equals(nameWithoutExtension, StringComparison.Ordinal));
    }

    private static bool IsNotExistingFolder(string? parentFolder, string name, bool ignoreCase,
        bool ignoreSameNameInParentFolder)
    {
        if (parentFolder is null)
            return true;

        var directory = new DirectoryInfo(parentFolder);
        if (!directory.Exists)
            return true;
        if (ignoreSameNameInParentFolder)
            return true;

        return !directory.EnumerateDirectories()
            .Select(d => d.Name)
            .Contains(name, ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }
}
