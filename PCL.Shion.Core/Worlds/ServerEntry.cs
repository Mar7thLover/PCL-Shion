namespace PCL.Shion.Core.Worlds;

/// <summary>
/// 服务器列表中的一项。
/// </summary>
/// <param name="Name">服务器名称；没有设置时用地址。</param>
/// <param name="Address">服务器地址，形如 host 或 host:port。</param>
public sealed record ServerEntry(string Name, string Address);
