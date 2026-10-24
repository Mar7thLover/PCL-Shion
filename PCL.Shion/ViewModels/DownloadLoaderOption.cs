using PCL.Shion.Core.Loaders;

namespace PCL.Shion.ViewModels;

/// <summary>加载器选择框中的一个选项。</summary>
/// <param name="Kind">加载器；为 null 表示不安装加载器。</param>
/// <param name="Label">显示的名称。</param>
public sealed record DownloadLoaderOption(LoaderKind? Kind, string Label)
{
    public override string ToString() => Label;
}
