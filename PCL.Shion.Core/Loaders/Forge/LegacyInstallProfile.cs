using System.Text.Json.Nodes;

namespace PCL.Shion.Core.Loaders.Forge;

/// <summary>
/// 旧版安装器（Forge 1.12.2 及更早）的 install_profile.json：install 描述安装器自带的 universal jar，
/// versionInfo 就是要写出的版本 JSON，没有处理器。
/// </summary>
/// <param name="UniversalEntry">安装器中 universal jar 的条目名。</param>
/// <param name="UniversalName">universal jar 作为库的 Maven 坐标，决定它在 libraries 下的位置。</param>
/// <param name="StripMeta">复制原版客户端 jar 时是否去掉 META-INF。</param>
/// <param name="VersionInfo">版本 JSON。</param>
internal sealed record LegacyInstallProfile(string UniversalEntry, string UniversalName, bool StripMeta, JsonObject VersionInfo)
{
    /// <summary>
    /// 内容是旧版安装器的配置时返回解析结果，否则（新版安装器）返回 null。
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">内容不是合法的 JSON。</exception>
    /// <exception cref="InvalidDataException">install 缺少必需的字段。</exception>
    public static LegacyInstallProfile? TryParse(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject root
            || root["install"] is not JsonObject install
            || root["versionInfo"] is not JsonObject versionInfo)
        {
            return null;
        }

        return new LegacyInstallProfile(
            install["filePath"]?.GetValue<string>()
                ?? throw new InvalidDataException("The install profile does not name its universal jar."),
            install["path"]?.GetValue<string>()
                ?? throw new InvalidDataException("The install profile does not give the library name of its universal jar."),
            install["stripMeta"]?.GetValue<bool>() ?? false,
            versionInfo);
    }
}
