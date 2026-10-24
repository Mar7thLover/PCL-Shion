using System.Text.Json;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Java.Runtime;

/// <summary>
/// 运行时清单文件的位置。
/// </summary>
/// <param name="Url">清单文件的官方地址。</param>
/// <param name="Sha1">清单文件的 SHA-1。</param>
internal sealed record RuntimeManifestRef(string Url, string? Sha1);

/// <summary>
/// 解析 Mojang 的 Java 运行时索引：平台 → 组件 → 版本列表 → 清单文件。
/// </summary>
internal static class RuntimeIndex
{
    /// <summary>
    /// 查找指定平台上某个组件的清单文件。该平台没有这个组件（缺失或列表为空）时抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    public static RuntimeManifestRef FindManifest(ReadOnlyMemory<byte> json, string platformKey, string component)
    {
        using var document = JsonDocument.Parse(json);
        var versions = document.RootElement.GetObjectProperty(platformKey)?.GetArrayProperty(component);
        var manifest = versions is { } list && list.GetArrayLength() > 0
            ? list[0].GetObjectProperty("manifest")
            : null;
        if (manifest?.GetStringProperty("url") is not { } url)
            throw new InvalidOperationException($"The Java runtime '{component}' is not available for '{platformKey}'.");

        return new RuntimeManifestRef(url, manifest.Value.GetStringProperty("sha1"));
    }
}
