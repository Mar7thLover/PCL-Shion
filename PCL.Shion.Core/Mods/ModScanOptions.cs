namespace PCL.Shion.Core.Mods;

/// <summary>单个 Mod 的读取预算。关闭解析时只返回文件名和启用状态，不读取内容或计算哈希。</summary>
public sealed record ModScanOptions
{
    public bool ParseMetadata { get; init; } = true;

    public int MaxNestedDepth { get; init; } = 8;

    public int MaxNestedJars { get; init; } = 256;

    public int MaxMetadataBytes { get; init; } = 1024 * 1024;

    public int MaxNestedJarBytes { get; init; } = 32 * 1024 * 1024;

    /// <summary>整棵嵌套树共用的解压预算，包含元数据和嵌套 Jar 的内容。</summary>
    public long MaxTotalUncompressedBytes { get; init; } = 64L * 1024 * 1024;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaxNestedDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(MaxNestedJars);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxMetadataBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxNestedJarBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxTotalUncompressedBytes);
    }
}
