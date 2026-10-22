using System.Text;

namespace PCL.Shion.Core.Test;

/// <summary>
/// 在临时目录里搭建一个假的 .minecraft，释放时整体删除。
/// </summary>
internal sealed class FakeMinecraftRoot : IDisposable
{
    public FakeMinecraftRoot()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"pcl-shion-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    /// <summary>
    /// 把以 / 分隔的相对路径转换成 .minecraft 下的完整路径。
    /// </summary>
    public string GetPath(string relativePath)
    {
        return Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public string WriteVersion(string name, string json)
    {
        return WriteFile($"versions/{name}/{name}.json", json);
    }

    public string WriteFile(string relativePath, string content = "", Encoding? encoding = null)
    {
        var path = GetPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, encoding ?? new UTF8Encoding(false));
        return path;
    }

    public void Dispose()
    {
        Directory.Delete(RootPath, recursive: true);
    }
}
