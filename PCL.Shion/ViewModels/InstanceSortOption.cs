using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

/// <summary>实例排序选择框中的一个选项。</summary>
public sealed record InstanceSortOption(InstanceSortMode Mode, string Label)
{
    public override string ToString() => Label;
}
