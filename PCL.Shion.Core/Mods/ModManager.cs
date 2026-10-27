using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Mods;

/// <summary>
/// 启用、禁用与删除 Mod 文件。
/// </summary>
public static class ModManager
{
    /// <summary>
    /// 通过 .disabled 后缀启用或禁用 Mod，返回更新了路径与状态的 Mod。状态已符合时不动文件。
    /// </summary>
    /// <exception cref="IOException">目标文件名已被占用。</exception>
    public static LocalMod SetEnabled(LocalMod mod, bool enabled)
    {
        if (mod.Enabled == enabled)
            return mod;

        var path = enabled
            ? Path.Combine(Path.GetDirectoryName(mod.FilePath) ?? "", ModScanner.GetEnabledFileName(mod.FileName))
            : mod.FilePath + ModScanner.DisabledSuffix;
        File.Move(mod.FilePath, path);
        return mod with { FilePath = path, FileName = Path.GetFileName(path), Enabled = enabled };
    }

    /// <summary>
    /// 把 Mod 文件移到系统回收站。
    /// </summary>
    public static void Delete(LocalMod mod) => Trash.MoveToTrash(mod.FilePath);
}
