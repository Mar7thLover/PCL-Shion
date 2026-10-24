namespace PCL.Shion.Core.Instances;

/// <summary>
/// 启动后直接进入的存档或服务器。
/// </summary>
/// <param name="Kind">目标类型。</param>
/// <param name="Value">单人存档的文件夹名，或多人服务器的 host[:port]。</param>
public sealed record QuickPlayTarget(QuickPlayKind Kind, string Value);
