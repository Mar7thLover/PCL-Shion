namespace PCL.Shion.Core.Loaders.Forge;

/// <summary>
/// Forge 或 NeoForge 安装器的处理器运行失败：Java 进程的退出码不为 0。消息中附有进程最后输出的几行。
/// </summary>
/// <param name="processor">处理器 jar 的 Maven 坐标。</param>
/// <param name="exitCode">进程的退出码。</param>
/// <param name="output">进程最后输出的几行，标准输出与标准错误合流。</param>
public sealed class ForgeProcessorException(string processor, int exitCode, IReadOnlyList<string> output)
    : Exception($"The installer processor {processor} exited with code {exitCode}." + string.Concat(output.Select(line => "\n" + line)))
{
    /// <summary>处理器 jar 的 Maven 坐标。</summary>
    public string Processor { get; } = processor;

    public int ExitCode { get; } = exitCode;

    /// <summary>进程最后输出的几行。</summary>
    public IReadOnlyList<string> Output { get; } = output;
}
