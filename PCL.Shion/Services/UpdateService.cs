using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Shion.Core.Updates;

namespace PCL.Shion.Services;

/// <summary>
/// 检查 PCL Shion 有没有新版本，并保存最近一次的结果。
/// <see cref="Changed"/> 在调用 <see cref="CheckAsync"/> 的线程上触发，从界面线程调用时即界面线程。
/// </summary>
/// <param name="settings">是否包含预发布版取自这里。</param>
/// <param name="http">取得发起请求用的 HTTP 客户端。</param>
public sealed class UpdateService(AppSettings settings, Func<HttpClient> http)
{
    public UpdateState State { get; private set; }

    /// <summary>状态为 <see cref="UpdateState.Available"/> 时的新版本。</summary>
    public ReleaseInfo? Release { get; private set; }

    /// <summary>状态为 <see cref="UpdateState.Failed"/> 时的失败原因。</summary>
    public string? Error { get; private set; }

    /// <summary>状态变化时触发。</summary>
    public event Action? Changed;

    /// <summary>是否把预发布版本计入：设置页的更新分支选择测试版（序号 1），或旧设置中勾选了包含预发布版。</summary>
    private bool IncludePrerelease => settings.SystemUpdateChannel == 1 || settings.IncludePrereleaseUpdates;

    /// <summary>
    /// 启动时是否自动检查更新。启动器不会自行下载或安装更新：
    /// “不自动检查更新”（序号 3）以外的选项一律只检查并弹窗提示。
    /// </summary>
    public bool CheckOnStartup => settings.SystemUpdateMode != 3;

    /// <summary>检查一次更新，结果通过状态给出，不会抛出网络错误。已有检查在进行时直接返回。</summary>
    public async Task CheckAsync()
    {
        if (State == UpdateState.Checking)
            return;

        Update(UpdateState.Checking);
        try
        {
            var latest = await UpdateChecker.GetLatestAsync(http(), IncludePrerelease, CancellationToken.None);
            if (latest is not null && UpdateChecker.IsNewer(latest, AppInfo.Version))
                Update(UpdateState.Available, release: latest);
            else
                Update(UpdateState.UpToDate);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or OperationCanceledException)
        {
            Update(UpdateState.Failed, error: e.Message);
        }
    }

    private void Update(UpdateState state, ReleaseInfo? release = null, string? error = null)
    {
        State = state;
        Release = release;
        Error = error;
        Changed?.Invoke();
    }
}
