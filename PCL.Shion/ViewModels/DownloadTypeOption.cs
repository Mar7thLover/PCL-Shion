using System.Collections.Generic;

namespace PCL.Shion.ViewModels;

/// <summary>类型筛选的一个选项。</summary>
/// <param name="Label">显示的名称。</param>
/// <param name="ManifestTypes">版本清单中属于该选项的 type 值。</param>
public sealed record DownloadTypeOption(string Label, IReadOnlyList<string> ManifestTypes)
{
    public override string ToString() => Label;
}
