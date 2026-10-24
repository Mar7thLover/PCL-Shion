namespace PCL.Shion.ViewModels;

/// <summary>默认 Java 选择框中的一个选项。</summary>
/// <param name="Executable">Java 可执行文件的完整路径；为 null 表示按实例需要自动选择。</param>
/// <param name="Label">显示的名称。</param>
public sealed record SettingsJavaOption(string? Executable, string Label)
{
    public override string ToString() => Label;
}
