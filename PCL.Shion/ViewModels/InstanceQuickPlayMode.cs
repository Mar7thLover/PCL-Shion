using PCL.Shion.Core.Instances;

namespace PCL.Shion.ViewModels;

/// <summary>快速进入方式选择框中的一项；<paramref name="Kind"/> 为 null 表示不使用快速进入。</summary>
public sealed record InstanceQuickPlayMode(QuickPlayKind? Kind, string Label)
{
    public override string ToString() => Label;
}
