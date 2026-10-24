namespace PCL.Shion.ViewModels;

/// <summary>Minecraft 目录列表中的一项。</summary>
/// <param name="Path">目录的完整路径。</param>
/// <param name="IsCurrent">是否为当前使用的目录，即列表的第一项。</param>
public sealed record SettingsFolderItem(string Path, bool IsCurrent);
