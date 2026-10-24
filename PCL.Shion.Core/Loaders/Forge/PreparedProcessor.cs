namespace PCL.Shion.Core.Loaders.Forge;

/// <summary>
/// 变量已经替换完毕、可以直接运行的处理器，其中的路径都是绝对路径。
/// </summary>
/// <param name="Jar">处理器 jar 的 Maven 坐标，用于说明是哪个处理器。</param>
/// <param name="JarPath">处理器 jar 的路径。</param>
/// <param name="Classpath">除处理器 jar 之外还要放进 classpath 的库。</param>
/// <param name="Arguments">传给主类的参数。</param>
/// <param name="Outputs">输出文件的路径到期望 SHA-1 的映射。</param>
internal sealed record PreparedProcessor(
    string Jar,
    string JarPath,
    IReadOnlyList<string> Classpath,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Outputs)
{
    /// <summary>
    /// 取形如 <c>--name value</c> 的参数的值，没有时返回 null。
    /// </summary>
    public string? GetOption(string name)
    {
        for (var i = 0; i < Arguments.Count - 1; i++)
        {
            if (Arguments[i] == name)
                return Arguments[i + 1];
        }
        return null;
    }
}
