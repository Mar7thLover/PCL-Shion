namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 库的一个文件（downloads.artifact 或 downloads.classifiers 中的一项）。
/// </summary>
/// <param name="Path">相对于 libraries 目录的路径，以 / 分隔；未声明时为 null。</param>
/// <param name="Url">下载地址。null 表示未声明；空字符串表示没有下载地址，例如 Forge 安装器在本地生成的库。</param>
/// <param name="Sha1">SHA-1 校验值，未声明时为 null。</param>
/// <param name="Size">文件大小（字节），未声明时为 null。</param>
public sealed record LibraryDownload(string? Path, string? Url, string? Sha1, long? Size);
