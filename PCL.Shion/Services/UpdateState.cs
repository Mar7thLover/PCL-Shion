namespace PCL.Shion.Services;

/// <summary>
/// 更新检查的状态。
/// </summary>
public enum UpdateState
{
    /// <summary>还没有检查过。</summary>
    Idle,

    /// <summary>正在检查。</summary>
    Checking,

    /// <summary>已是最新版本。</summary>
    UpToDate,

    /// <summary>有新版本可用。</summary>
    Available,

    /// <summary>检查失败。</summary>
    Failed,
}
