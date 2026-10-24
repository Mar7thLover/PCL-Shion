using System.Collections.Generic;

namespace PCL.Shion;

/// <summary>
///     内嵌（Jar-in-Jar）Mod 的解析支持，对应 WPF 版 ModJarInJarDependency 与 ModJarInJarCache 的入口。
///     TODO(shion): 内嵌 Mod 的嵌套扫描、依赖解析与缓存文件尚未移植，以下类型目前只提供接口，不执行任何解析。
/// </summary>
public static class ModJarInJar
{
    /// <summary>
    ///     一次内嵌解析会话的上下文。
    /// </summary>
    public sealed class ScanContext
    {
    }
}

/// <summary>
///     内嵌 Mod 解析结果的缓存。TODO(shion): 缓存文件尚未接入，所有方法当前均为空操作。
/// </summary>
public static class ModJarInJarCache
{
    /// <summary>切换到指定实例的缓存。</summary>
    public static void UseInstance(string? instancePath)
    {
    }

    /// <summary>移除不再存在的文件的缓存项。</summary>
    public static void Prune(IEnumerable<string> existingPaths)
    {
    }

    /// <summary>将缓存写入磁盘。</summary>
    public static void Flush()
    {
    }
}
