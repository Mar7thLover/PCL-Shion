using System.Globalization;
using PCL.Shion.Core.Versions;

namespace PCL.Shion.ViewModels;

/// <summary>版本列表中的一项。</summary>
public sealed class DownloadVersionItem(RemoteVersion version)
{
    public RemoteVersion Version => version;

    public string Id => version.Id;

    public string TypeText => version.Type switch
    {
        "release" => DownloadStrings.TypeRelease,
        "snapshot" => DownloadStrings.TypeSnapshot,
        "old_beta" => DownloadStrings.TypeOldBeta,
        "old_alpha" => DownloadStrings.TypeOldAlpha,
        _ => version.Type,
    };

    public string DateText => version.ReleaseTime.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>已选版本的说明：类型与发布日期。</summary>
    public string Description => $"{TypeText} · {DateText}";
}
