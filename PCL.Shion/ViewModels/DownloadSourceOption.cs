using PCL.Shion.Core.Sources;

namespace PCL.Shion.ViewModels;

/// <summary>下载源选择框中的一个选项。</summary>
public sealed record DownloadSourceOption(DownloadSource Source, string Label)
{
    public override string ToString() => Label;
}
