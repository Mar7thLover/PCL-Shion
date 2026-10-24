namespace PCL.Shion.Core.Loaders.Forge;

/// <summary>
/// install_profile.json 中的一个处理器：安装时运行的 Java 程序，参数与输出都是含变量的模板。
/// </summary>
/// <param name="Jar">处理器 jar 的 Maven 坐标，主类取自它的清单。</param>
/// <param name="Classpath">运行时还要放进 classpath 的库的 Maven 坐标。</param>
/// <param name="Arguments">参数模板。</param>
/// <param name="Outputs">输出文件模板到其 SHA-1 模板的映射。</param>
/// <param name="Sides">在哪一侧运行（client / server）；null 表示两侧都运行。</param>
internal sealed record InstallProcessor(
    string Jar,
    IReadOnlyList<string> Classpath,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Outputs,
    IReadOnlyList<string>? Sides)
{
    public bool RunsOnClient => Sides is null || Sides.Contains("client");

    /// <summary>
    /// 用 <paramref name="variables"/> 替换参数与输出中的变量，把 Maven 坐标换成 <paramref name="librariesDirectory"/> 下的路径。
    /// </summary>
    /// <exception cref="InvalidDataException">模板格式错误，或引用了未定义的变量。</exception>
    public PreparedProcessor Prepare(string librariesDirectory, IReadOnlyDictionary<string, string> variables)
    {
        return new PreparedProcessor(
            Jar,
            ProcessorTokens.GetLibraryPath(librariesDirectory, Jar),
            Classpath.Select(name => ProcessorTokens.GetLibraryPath(librariesDirectory, name)).ToList(),
            Arguments.Select(argument => ProcessorTokens.ResolveArgument(argument, librariesDirectory, variables)).ToList(),
            Outputs.ToDictionary(
                output => ProcessorTokens.Replace(output.Key, variables),
                output => ProcessorTokens.Replace(output.Value, variables)));
    }
}
