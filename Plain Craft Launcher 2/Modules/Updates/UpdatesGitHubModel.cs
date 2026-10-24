using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

/// <summary>
///     PCL Shion 的更新源：读取 GitHub 仓库的 Releases。
///     正式版通道只看非预发布的版本，测试版通道包含预发布版本；按架构选取发布流程上传的 PCL_Shion_*_x64.exe / _ARM64.exe。
/// </summary>
public class UpdatesGitHubModel : IUpdateSource
{
    private const string ReleasesApi = "https://api.github.com/repos/Mar7thLover/PCL-Shion/releases?per_page=30";

    private List<GitHubRelease> _releases;

    public string SourceName { get; set; } = "GitHub";

    public bool IsAvailable()
    {
        return true;
    }

    public bool RefreshCache()
    {
        var json = Requester.FetchString(ReleasesApi, RequestParam.WithRetry);
        var releases = new List<GitHubRelease>();
        foreach (var node in ModBase.GetJson(json).AsArray())
        {
            if (node is null || (bool?)node["draft"] == true)
                continue;
            var tag = ((string)node["tag_name"] ?? "").TrimStart('v', 'V');
            if (!SemVer.TryParse(tag, out var version))
                continue;
            var assets = node["assets"]?.AsArray()
                .Where(asset => asset is not null)
                .Select(asset => new GitHubAsset(
                    (string)asset["name"] ?? "",
                    (string)asset["browser_download_url"] ?? "",
                    ((string)asset["digest"] ?? "").Replace("sha256:", "")))
                .ToList() ?? [];
            releases.Add(new GitHubRelease(version!, tag, (bool?)node["prerelease"] == true,
                (string)node["body"] ?? "", assets));
        }

        _releases = releases;
        return true;
    }

    public VersionDataModel GetLatestVersion(UpdateChannel channel, UpdateArch arch)
    {
        var (release, asset) = GetLatest(channel, arch);
        return new VersionDataModel
        {
            VersionName = release.Tag,
            VersionCode = 0,
            Sha256 = asset.Sha256,
            Source = SourceName,
            Changelog = release.Body
        };
    }

    public bool IsLatest(UpdateChannel channel, UpdateArch arch, SemVer currentVersion, int currentVersionCode)
    {
        return currentVersion >= GetLatest(channel, arch).Release.Version;
    }

    public VersionAnnouncementDataModel GetAnnouncementList()
    {
        // PCL Shion 不发布启动器内公告
        return new VersionAnnouncementDataModel { Content = [] };
    }

    public List<ModLoader.LoaderBase> GetDownloadLoader(UpdateChannel channel, UpdateArch arch, string output)
    {
        return
        [
            new ModLoader.LoaderTask<int, List<DownloadFile>>(Lang.Text("Update.Task.GetVersionInfo"), load =>
            {
                var (_, asset) = GetLatest(channel, arch);
                load.output = [new DownloadFile([asset.Url], output)];
            }),
            new LoaderDownload(Lang.Text("Update.Task.DownloadFile"), [])
        ];
    }

    private (GitHubRelease Release, GitHubAsset Asset) GetLatest(UpdateChannel channel, UpdateArch arch)
    {
        if (_releases is null)
            RefreshCache();
        var suffix = arch == UpdateArch.arm64 ? "_ARM64.exe" : "_x64.exe";
        foreach (var release in _releases!
                     .Where(r => channel == UpdateChannel.beta || !r.IsPrerelease)
                     .OrderByDescending(r => r.Version))
        {
            var asset = release.Assets.FirstOrDefault(a =>
                a.Name.StartsWith("PCL_Shion_", StringComparison.OrdinalIgnoreCase) &&
                a.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            if (asset is not null)
                return (release, asset);
        }

        throw new NullReferenceException("Can not get remote update info!");
    }

    private sealed record GitHubAsset(string Name, string Url, string Sha256);

    private sealed record GitHubRelease(SemVer Version, string Tag, bool IsPrerelease, string Body, List<GitHubAsset> Assets);
}
