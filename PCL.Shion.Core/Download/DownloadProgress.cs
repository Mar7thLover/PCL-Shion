namespace PCL.Shion.Core.Download;

/// <summary>
/// 一批下载的进度快照。字节数只统计大小已知的文件，大小未知的文件只计入文件数与速度。
/// </summary>
/// <param name="TotalFiles">文件总数，目标路径相同的项只算一个。</param>
/// <param name="CompletedFiles">已完成的文件数，包括无需下载的已有文件。</param>
/// <param name="FailedFiles">所有地址都失败的文件数。</param>
/// <param name="TotalBytes">大小已知的文件的总字节数。</param>
/// <param name="DownloadedBytes">其中已完成与正在传输的字节数。</param>
/// <param name="BytesPerSecond">最近约 2 秒的网络传输速度，包括失败重试消耗的流量。</param>
public sealed record DownloadProgress(
    int TotalFiles,
    int CompletedFiles,
    int FailedFiles,
    long TotalBytes,
    long DownloadedBytes,
    double BytesPerSecond);
