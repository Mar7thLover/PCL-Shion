namespace PCL.Shion.ViewModels;

/// <summary>Java 选择框中的一项；<paramref name="Executable"/> 为 null 表示自动选择。</summary>
public sealed record InstanceJavaOption(string? Executable, string Label)
{
    public override string ToString() => Label;
}
