namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 处理完 inheritsFrom 继承链后的版本信息。库与参数保持原始形态（含规则），由启动流程按当前环境求值。
/// </summary>
public sealed class ResolvedVersion
{
    /// <summary>实例名称（版本目录名）。</summary>
    public required string Id { get; init; }

    public required string MainClass { get; init; }

    /// <summary>release、snapshot 等；版本 JSON 未声明时为 null。</summary>
    public string? Type { get; init; }

    /// <summary>资源索引 ID；版本 JSON 未声明时为 legacy。</summary>
    public required string AssetIndexId { get; init; }

    /// <summary>版本要求的 Java 主版本号；未声明时为 null。</summary>
    public int? JavaMajorVersion { get; init; }

    /// <summary>客户端 jar 的完整路径，文件不一定存在。</summary>
    public required string ClientJarPath { get; init; }

    /// <summary>原版 Minecraft 版本号，无法确定时为 null。</summary>
    public string? MinecraftVersion { get; init; }

    /// <summary>forge / neoforge / fabric / quilt，无法识别时为 null。</summary>
    public string? Loader { get; init; }

    /// <summary>按先父后子顺序合并的全部库，未经规则筛选与去重。</summary>
    public required IReadOnlyList<VersionLibrary> Libraries { get; init; }

    public required IReadOnlyList<VersionArgument> JvmArguments { get; init; }

    public required IReadOnlyList<VersionArgument> GameArguments { get; init; }

    /// <summary>旧格式的 minecraftArguments（以空格分隔的整串）。</summary>
    public string? LegacyMinecraftArguments { get; init; }

    /// <summary>logging.client.argument，含 ${path} 占位符。</summary>
    public string? LoggingArgument { get; init; }

    /// <summary>logging.client.file.id，即 assets/log_configs 下的文件名。</summary>
    public string? LoggingFileId { get; init; }
}
