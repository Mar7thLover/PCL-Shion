namespace PCL.Shion.Core.Launch;

/// <summary>
/// 启动游戏所需的完整命令。
/// </summary>
/// <param name="JavaExecutable">Java 可执行文件的完整路径。</param>
/// <param name="Arguments">传给 Java 的参数，不含可执行文件本身。</param>
/// <param name="WorkingDirectory">游戏目录，同时作为进程工作目录。</param>
/// <param name="NativesDirectory">本地库目录。</param>
/// <param name="MissingFiles">启动所需但不存在的库、客户端 jar 与资源文件；非空时游戏很可能无法启动。</param>
public sealed record LaunchPlan(
    string JavaExecutable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    string NativesDirectory,
    IReadOnlyList<string> MissingFiles);
