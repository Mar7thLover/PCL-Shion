using PCL.Shion.Core.Download;

namespace PCL.Shion.Core.Install;

/// <summary>
/// 安装或补全的进度。
/// </summary>
/// <param name="Stage">当前阶段："version"（版本 JSON）、"files"（客户端 jar、库、资源索引与日志配置）、"assets"（资源对象）或 "java"（Java 运行时）。</param>
/// <param name="Download">本阶段的下载进度；阶段刚开始、尚无下载进度时为 null。</param>
public sealed record InstallProgress(string Stage, DownloadProgress? Download);
