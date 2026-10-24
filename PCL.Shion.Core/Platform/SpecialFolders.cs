namespace PCL.Shion.Core.Platform;

/// <summary>
/// 系统特殊目录。<see cref="Environment.GetFolderPath(Environment.SpecialFolder)" /> 在目录尚不存在时返回空字符串，
/// 例如 Linux 新账户还没有 ~/.config，拼出的路径就成了相对路径，随工作目录变化。这里总是返回绝对路径。
/// </summary>
public static class SpecialFolders
{
    /// <summary>
    /// 取得特殊目录的绝对路径。目录可能尚未创建，写入前需由调用方创建。
    /// 系统给不出绝对路径时（例如 HOME 为相对路径），退回到程序所在目录。
    /// </summary>
    public static string Get(Environment.SpecialFolder folder)
    {
        var path = Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);
        return Path.IsPathFullyQualified(path) ? path : AppContext.BaseDirectory;
    }
}
