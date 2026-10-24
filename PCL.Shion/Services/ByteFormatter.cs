namespace PCL.Shion.Services;

/// <summary>
/// 把字节数与速度格式化成界面上显示的文字。
/// </summary>
public static class ByteFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB"];

    /// <summary>
    /// 换算成 1024 进制的合适单位，如 <c>512 B</c>、<c>1.5 MB</c>：字节不带小数，其余保留一位小数。
    /// </summary>
    public static string Format(double bytes)
    {
        var unit = 0;
        while (bytes >= 1024 && unit < Units.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes:F0} {Units[unit]}" : $"{bytes:F1} {Units[unit]}";
    }

    /// <summary>
    /// 每秒字节数的文字，如 <c>1.5 MB/s</c>。
    /// </summary>
    public static string FormatSpeed(double bytesPerSecond) => $"{Format(bytesPerSecond)}/s";
}
