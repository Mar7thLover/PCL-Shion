using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Select;

/// <summary>
///     Minecraft 文件夹列表的读取与修改，对应 WPF 中 ModFolder 的列表操作。
///     列表第一项即当前使用的文件夹，见 <see cref="PCL.Shion.Services.AppSettings.MinecraftFolders" />。
/// </summary>
internal static class SelectFolders
{
    private static readonly StringComparer PathComparer =
        PlatformInfo.Os == OsKind.Linux ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    /// <summary>
    ///     去掉末尾分隔符后的完整路径。
    /// </summary>
    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public static bool IsSamePath(string? left, string? right) =>
        left is not null && right is not null && PathComparer.Equals(Normalize(left), Normalize(right));

    /// <summary>
    ///     是否为官方启动器的 .minecraft 文件夹。
    /// </summary>
    public static bool IsOfficial(string path) => IsSamePath(path, MinecraftFolder.GetDefault());

    /// <summary>
    ///     文件夹是否设置过自定义名称。
    /// </summary>
    public static bool HasCustomName(string path) =>
        App.Services.Settings.MinecraftFolderNames.ContainsKey(Normalize(path));

    /// <summary>
    ///     文件夹的显示名称：自定义名称优先，其次是官方文件夹的固定名称，最后根据路径推断。
    /// </summary>
    public static string GetName(string path)
    {
        if (App.Services.Settings.MinecraftFolderNames.TryGetValue(Normalize(path), out var name))
            return name;
        return IsOfficial(path) ? Lang.Text("Select.Folder.OfficialLauncherFolder") : DefaultName(path);
    }

    /// <summary>
    ///     根据路径推断文件夹名称：取最后一级目录名；若为 .minecraft，则取其上一级目录名。
    /// </summary>
    public static string DefaultName(string path)
    {
        var names = Normalize(path).Split(Path.DirectorySeparatorChar);
        var defaultName = names[^1] == ".minecraft"
            ? names.Length >= 3 ? names[^2] : ""
            : names[^1];
        if (defaultName.Length > 40)
            defaultName = defaultName[..39];
        return defaultName;
    }

    /// <summary>
    ///     修改目录列表并保存；当前目录因此变化时，通知各页面重新读取实例。
    /// </summary>
    public static void ChangeFolders(Action<List<string>> change)
    {
        var settings = App.Services.Settings;
        var previousRoot = settings.MinecraftRoot;
        change(settings.MinecraftFolders);
        if (settings.MinecraftFolders.Count == 0)
            settings.MinecraftFolders.Add(MinecraftFolder.GetDefault());
        settings.Save();
        if (!IsSamePath(settings.MinecraftRoot, previousRoot))
            App.Services.NotifyInstancesChanged();
    }

    /// <summary>
    ///     将文件夹设为当前目录，即列表的第一项；不在列表中时加入。
    /// </summary>
    public static void MakeCurrent(string path)
    {
        ChangeFolders(folders =>
        {
            var stored = folders.FirstOrDefault(folder => IsSamePath(folder, path)) ?? path;
            folders.RemoveAll(folder => IsSamePath(folder, path));
            folders.Insert(0, stored);
        });
    }

    /// <summary>
    ///     上移或下移文件夹，<paramref name="offset" /> 为 -1 或 1。
    /// </summary>
    public static void Move(string path, int offset)
    {
        ChangeFolders(folders =>
        {
            var index = folders.FindIndex(folder => IsSamePath(folder, path));
            var target = index + offset;
            if (index < 0 || target < 0 || target >= folders.Count)
                return;
            (folders[index], folders[target]) = (folders[target], folders[index]);
        });
    }

    /// <summary>
    ///     拖拽排序：把 <paramref name="source" /> 移到 <paramref name="target" /> 所在的位置。
    /// </summary>
    public static void Reorder(string source, string target)
    {
        ChangeFolders(folders =>
        {
            var sourceIndex = folders.IndexOf(source);
            var targetIndex = folders.IndexOf(target);
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
                return;
            folders.RemoveAt(sourceIndex);
            folders.Insert(Math.Clamp(targetIndex, 0, folders.Count), source);
        });
    }

    /// <summary>
    ///     为文件夹设置自定义名称。
    /// </summary>
    public static void Rename(string path, string name)
    {
        App.Services.Settings.MinecraftFolderNames[Normalize(path)] = name;
        App.Services.Settings.Save();
    }

    /// <summary>
    ///     清除自定义名称，恢复为默认名称。
    /// </summary>
    public static void RestoreName(string path)
    {
        App.Services.Settings.MinecraftFolderNames.Remove(Normalize(path));
        App.Services.Settings.Save();
    }

    /// <summary>
    ///     从列表中移除文件夹，不删除磁盘上的任何内容。
    /// </summary>
    public static void RemoveFolder(string path)
    {
        ChangeFolders(folders =>
        {
            folders.RemoveAll(folder => IsSamePath(folder, path));
            App.Services.Settings.MinecraftFolderNames.Remove(Normalize(path));
        });
    }

    /// <summary>
    ///     清理启动器在文件夹中生成的配置，对应 WPF 中删除列表项时的清理。
    /// </summary>
    public static void CleanupConfig(string folder)
    {
        var config = Path.Combine(folder, "PCL.ini");
        if (File.Exists(config))
            File.Delete(config);

        var versions = Path.Combine(folder, "versions");
        if (!Directory.Exists(versions))
            return;
        foreach (var version in new DirectoryInfo(versions).EnumerateDirectories())
        {
            var pclFolder = Path.Combine(version.FullName, "PCL");
            if (Directory.Exists(pclFolder))
                Directory.Delete(pclFolder, true);
        }
    }

    /// <summary>
    ///     将指定文件夹添加到列表，并设为当前目录。
    /// </summary>
    public static void AddFolder(string folderPath, bool showHint)
    {
        Task.Run(() =>
        {
            try
            {
                var folder = Normalize(folderPath);
                if (!CheckPermission(folder))
                {
                    if (!showHint)
                        throw new UnauthorizedAccessException("PCL 没有访问文件夹的权限：" + folder);
                    Hint.Show(Lang.Text("Select.Folder.AccessDenied"), HintType.Error);
                    return;
                }

                // 检查实际的 Minecraft 文件夹位置（没有问题，或是在子文件夹中）
                if (!CheckPermission(Path.Combine(folder, "versions")))
                    foreach (var child in new DirectoryInfo(folder).GetDirectories())
                        if (CheckPermission(Path.Combine(child.FullName, "versions")))
                        {
                            folder = Normalize(child.FullName);
                            break;
                        }

                // 判断是否已经添加过
                if (App.Services.Settings.MinecraftFolders.Any(item => IsSamePath(item, folder)))
                {
                    if (showHint)
                        Hint.Show(Lang.Text("Select.Folder.AlreadyInList"));
                    return;
                }

                // 如果没有添加过，则添加进去并切换选择
                MakeCurrent(folder);
                if (showHint)
                    Hint.Show(Lang.Text("Select.Folder.Added", GetName(folder)), HintType.Success);

                // 检查是否为根目录整合包，自动关闭版本隔离
                DisableIsolationForModpack(folder);
            }
            catch (Exception ex)
            {
                SelectLog.Feedback(Lang.Text("Select.Folder.Error.AddNew"), ex);
            }
        });
    }

    /// <summary>
    ///     在官方 .minecraft 位置创建文件夹，并设为当前目录。
    /// </summary>
    public static void CreateNew()
    {
        var root = Normalize(MinecraftFolder.GetDefault());
        if (!Directory.Exists(root))
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "versions"));
            MakeCurrent(root);
            CreateLauncherProfiles(root);
            Hint.Show(Lang.Text("Select.Folder.CreateSuccess"), HintType.Success);
        }

        App.Services.NotifyInstancesChanged();
    }

    /// <summary>
    ///     检查当前进程能否读写指定文件夹。
    /// </summary>
    public static bool CheckPermission(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
                return false;
            var test = Path.Combine(folder, $".pcl-permission-{Guid.NewGuid():N}");
            File.WriteAllText(test, "");
            File.Delete(test);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    ///     整合包根目录中，若所有可安装模组的实例都没有自己的 mods 文件夹，则关闭这些实例的版本隔离。
    /// </summary>
    private static void DisableIsolationForModpack(string folder)
    {
        var modFolder = new DirectoryInfo(Path.Combine(folder, "mods"));
        if (!(modFolder.Exists && modFolder.EnumerateFiles().Count() >= 3))
            return;
        var versionFolder = new DirectoryInfo(Path.Combine(folder, "versions"));
        if (!(versionFolder.Exists && versionFolder.EnumerateDirectories().Count() <= 3))
            return;

        var modable = InstanceScanner.Scan(folder).Where(instance => instance.Loader is not null).ToList();
        foreach (var instance in modable)
        {
            var indieModFolder = new DirectoryInfo(Path.Combine(instance.Directory, "mods"));
            if (indieModFolder.Exists && indieModFolder.EnumerateFiles().Any())
                return;
        }

        foreach (var instance in modable)
        {
            var settings = InstanceStore.Load(folder, instance.Name);
            settings.Isolated = false;
            InstanceStore.Save(folder, instance.Name, settings);
            SelectLog.Info("已自动关闭单版本隔离：" + instance.Name);
        }
    }

    private static void CreateLauncherProfiles(string folder)
    {
        try
        {
            var path = Path.Combine(folder, "launcher_profiles.json");
            if (File.Exists(path))
                return;
            var now = DateTime.Now;
            var date = now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var time = now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            var profile = new JsonObject
            {
                ["icon"] = "Grass",
                ["name"] = "PCL",
                ["lastVersionId"] = "latest-release",
                ["type"] = "latest-release",
                ["lastUsed"] = $"{date}T{time}.0000Z"
            };
            var profiles = new JsonObject
            {
                ["profiles"] = new JsonObject { ["PCL"] = profile },
                ["selectedProfile"] = "PCL",
                ["clientToken"] = "23323323323323323323323323323333"
            };
            File.WriteAllText(path, profiles.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
            SelectLog.Info("已创建 launcher_profiles.json：" + folder);
        }
        catch (Exception ex)
        {
            SelectLog.Error("创建 launcher_profiles.json 失败（" + folder + "）", ex);
        }
    }
}
