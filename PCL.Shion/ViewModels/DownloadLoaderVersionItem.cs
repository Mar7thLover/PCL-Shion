using PCL.Shion.Core.Loaders;

namespace PCL.Shion.ViewModels;

/// <summary>加载器版本选择框中的一项。</summary>
public sealed class DownloadLoaderVersionItem(LoaderVersion version)
{
    public LoaderVersion Version => version;

    /// <summary>安装加载器时的默认实例名：<c>&lt;原版号&gt;-&lt;加载器&gt;-&lt;加载器版本&gt;</c>。</summary>
    public string DefaultInstanceName => $"{version.MinecraftVersion}-{version.Kind.ToString().ToLowerInvariant()}-{version.Version}";

    public override string ToString() => version.Stable ? version.Version : $"{version.Version} · {DownloadStrings.LoaderUnstable}";
}
