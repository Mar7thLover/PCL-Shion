using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PCL.Shion.Core.Download;
using PCL.Shion.Navigation;

namespace PCL.Shion.Pages.Speed;

/// <summary>
///     任务管理页的数据来源。<see cref="Loaders" /> 对应 WPF 的 ModLoader.loaderTaskbar，
///     下载统计对应 WPF 的 ModNet.NetManager（速度、剩余文件、活动连接）。
///     正在执行的下载通过 <see cref="ReportDownload" /> 汇报进度，结束时以 null 撤销。
/// </summary>
public static class SpeedTaskbar
{
    private static readonly Lock Gate = new();
    private static readonly List<ModLoader.LoaderBase> Tasks = [];
    private static readonly Dictionary<object, DownloadProgress> Downloads = [];

    /// <summary>当前任务列表的快照。</summary>
    public static IReadOnlyList<ModLoader.LoaderBase> Loaders
    {
        get
        {
            lock (Gate)
                return [.. Tasks];
        }
    }

    public static void Add(ModLoader.LoaderBase loader)
    {
        lock (Gate)
            if (!Tasks.Contains(loader))
                Tasks.Add(loader);
    }

    public static void Remove(ModLoader.LoaderBase loader)
    {
        lock (Gate)
            Tasks.Remove(loader);
    }

    /// <summary>汇报一次下载的进度；<paramref name="progress" /> 为 null 时撤销该下载。</summary>
    public static void ReportDownload(object owner, DownloadProgress? progress)
    {
        lock (Gate)
        {
            if (progress is null)
                Downloads.Remove(owner);
            else
                Downloads[owner] = progress;
        }
    }

    /// <summary>同时进行的连接数上限，对应 WPF 的 ModNet.NetTaskConnectionLimit。</summary>
    public static int ConnectionLimit => App.Services.Settings.MaxDownloadConcurrency;

    /// <summary>所有下载的合计速度，单位为字节每秒。</summary>
    public static double Speed
    {
        get
        {
            lock (Gate)
                return Downloads.Values.Sum(p => p.BytesPerSecond);
        }
    }

    /// <summary>剩余文件数。</summary>
    public static int FileRemain
    {
        get
        {
            lock (Gate)
                return Downloads.Values.Sum(p => Remaining(p));
        }
    }

    /// <summary>
    ///     活动连接数。下载引擎不公开实际连接数，这里按各下载的剩余文件数估算，不超过 <see cref="ConnectionLimit" />。
    /// </summary>
    public static int ConnectionCount
    {
        get
        {
            var limit = ConnectionLimit;
            lock (Gate)
                return Math.Min(limit, Downloads.Values.Sum(p => Math.Min(limit, Remaining(p))));
        }
    }

    private static int Remaining(DownloadProgress progress) =>
        Math.Max(0, progress.TotalFiles - progress.CompletedFiles - progress.FailedFiles);
}
