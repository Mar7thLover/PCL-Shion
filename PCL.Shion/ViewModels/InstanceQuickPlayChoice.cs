namespace PCL.Shion.ViewModels;

/// <summary>快速进入目标选择框中的一项：存档的文件夹名，或服务器地址。</summary>
public sealed record InstanceQuickPlayChoice(string Value, string Label)
{
    public override string ToString() => Label;
}
