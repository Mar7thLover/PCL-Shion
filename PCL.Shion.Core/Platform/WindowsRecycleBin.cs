using System.Runtime.InteropServices;

namespace PCL.Shion.Core.Platform;

/// <summary>
/// 用 SHFileOperation 把文件或目录移入 Windows 回收站。
/// </summary>
internal static class WindowsRecycleBin
{
    private const uint FoDelete = 0x0003;

    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;
    private const ushort FofWantNukeWarning = 0x4000;

    // FOF_WANTNUKEWARNING 让回收站放不下的文件仍会向用户确认，而不是被 FOF_NOCONFIRMATION 直接永久删除
    private const ushort MoveToRecycleBinFlags = FofAllowUndo | FofNoConfirmation | FofNoErrorUi | FofSilent | FofWantNukeWarning;

    /// <summary>
    /// 把 <paramref name="fullPath"/> 移入回收站。用户拒绝永久删除的确认后文件仍在原处，同样视为失败。
    /// </summary>
    /// <param name="fullPath">完整路径；相对路径会被系统当作永久删除处理。</param>
    /// <exception cref="IOException">没有移入回收站。</exception>
    public static void Move(string fullPath)
    {
        var operation = new ShFileOpStruct
        {
            Func = FoDelete,
            // 源路径列表以两个空字符结尾：封送字符串时会补上一个，这里再补一个
            From = fullPath + '\0',
            Flags = MoveToRecycleBinFlags
        };

        var error = SHFileOperation(ref operation);

        // 不读取 AnyOperationsAborted，它在 32 位系统上的原生偏移与这里不同，所以改为确认路径已经不存在
        if (error != 0 || Path.Exists(fullPath))
            throw new IOException($"Failed to move '{fullPath}' to the Recycle Bin (error 0x{error:X}).");
    }

    // Core 项目没有启用 AllowUnsafeBlocks，而 LibraryImport 生成的代码需要它，所以这里使用 DllImport
    [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref ShFileOpStruct fileOperation);

    // 只用到 Func、From、Flags，它们在 32 位与 64 位下的偏移一致，所以一个结构体通用；其余字段系统不会读取，保持为零
#pragma warning disable CS0649
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public nint Hwnd;
        public uint Func;
        public string? From;
        public string? To;
        public ushort Flags;
        public int AnyOperationsAborted;
        public nint NameMappings;
        public string? ProgressTitle;
    }
#pragma warning restore CS0649
}
