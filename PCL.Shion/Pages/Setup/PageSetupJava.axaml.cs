using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PCL.Shion.Controls;
using PCL.Shion.Core.Java;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Setup;

public partial class PageSetupJava : MyPageRight
{
    /// <summary>
    ///     一个可用的 Java 可执行文件。
    /// </summary>
    public sealed record JavaEntry(
        string Executable,
        int MajorVersion,
        string Version,
        bool Is64Bit,
        string Source,
        bool IsJdk,
        string JavaFolder);

    private static readonly object entryLock = new();
    private static List<JavaEntry>? entryCache;

    private static AppSettings Settings => App.Services.Settings;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public ModLoader.LoaderTask<bool, List<JavaEntry>> loader;

    public PageSetupJava()
    {
        InitializeComponent();
        loader = new ModLoader.LoaderTask<bool, List<JavaEntry>>("JavaPageLoader", Load_GetJavaList);
        Loaded += PageSetupJava_Loaded;
    }

    private void PageSetupJava_Loaded(object? sender, RoutedEventArgs e)
    {
        PageLoaderInit(PanLoad, CardLoad, PanMain, null, loader, _ => OnLoadFinished(), Load_Input);
    }

    private object? Load_Input()
    {
        return false;
    }

    private static void Load_GetJavaList(ModLoader.LoaderTask<bool, List<JavaEntry>> loader)
    {
        if (loader.input || loader.isForceRestarting || GetEntries() is null)
            SetEntries(BuildEntries(JavaLocator.FindAll()));
        loader.output = SortEntries(GetEntries() ?? new List<JavaEntry>());
    }

    private void OnLoadFinished()
    {
        PanContent.Children.Clear();
        var itemAuto = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox,
            Title = Lang.Text("Setup.Java.AutoSelect.Title"),
            Info = Lang.Text("Setup.Java.AutoSelect.Info")
        };
        itemAuto.Check += (_, _) =>
        {
            Settings.DefaultJavaExecutable = null;
            Settings.Save();
        };
        PanContent.Children.Add(itemAuto);
        var currentSetJava = Settings.DefaultJavaExecutable;
        foreach (var entry in loader.output ?? new List<JavaEntry>())
        {
            var item = ItemBuild(entry);
            PanContent.Children.Add(item);
            if (PathComparer.Equals(entry.Executable, currentSetJava ?? ""))
                item.SetChecked(true, false, false);
        }

        if (string.IsNullOrEmpty(currentSetJava))
            itemAuto.SetChecked(true, false, false);
    }

    private MyListItem ItemBuild(JavaEntry j)
    {
        var item = new MyListItem();
        var versionTypeDesc = j.IsJdk ? "JDK" : "JRE";
        var versionNameDesc = j.MajorVersion.ToString();
        item.Title = $"{versionTypeDesc} {versionNameDesc}";

        item.Info = j.JavaFolder;
        var displayBits = j.Is64Bit ? "64 Bit" : "32 Bit";
        var displayBrand = j.Source;
        item.Tags = new List<string> { displayBits, displayBrand };

        item.Type = MyListItem.CheckType.RadioBox;
        item.Check += (_, e) =>
        {
            if (!IsAvailable(j))
            {
                HintService.Hint(Lang.Text("Setup.Java.Unavailable"));
                return;
            }

            if (IsJavaEnabled(j.Executable))
            {
                Settings.DefaultJavaExecutable = j.Executable;
                Settings.Save();
            }
            else
            {
                HintService.Hint(Lang.Text("Setup.Java.EnableBeforeSelect"));
                e.handled = true;
            }
        };
        var btnOpenFolder = new MyIconButton
        {
            SvgIcon = "lucide/folder-open"
        };
        ToolTip.SetTip(btnOpenFolder, Lang.Text("Common.Action.Open"));
        btnOpenFolder.Click += (_, _) =>
        {
            if (!IsAvailable(j))
            {
                HintService.Hint(Lang.Text("Setup.Java.Unavailable"));
                return;
            }

            SystemShell.OpenFolder(j.JavaFolder);
        };
        var btnInfo = new MyIconButton
        {
            SvgIcon = "lucide/info"
        };
        ToolTip.SetTip(btnInfo, Lang.Text("Setup.Java.Detail.ToolTip"));
        btnInfo.Click += (_, _) =>
        {
            if (!IsAvailable(j))
            {
                HintService.Hint(Lang.Text("Setup.Java.Unavailable"));
                return;
            }

            _ = MsgBox.ShowAsync(
                Lang.Text("Setup.Java.Info.Format",
                    versionTypeDesc,
                    j.Version,
                    j.Is64Bit ? "x64" : "x86",
                    displayBits,
                    displayBrand,
                    j.JavaFolder),
                Lang.Text("Setup.Java.Info.Title"));
        };
        var btnEnableSwitch = new MyIconButton();
        item.Buttons = [btnOpenFolder, btnInfo, btnEnableSwitch];

        var title = FindTitleLabel(item);

        void UpdateEnableStyle(bool isCurEnable)
        {
            if (!IsAvailable(j))
            {
                HintService.Hint(Lang.Text("Setup.Java.Unavailable"));
                return;
            }

            if (title is not null)
            {
                if (isCurEnable)
                {
                    title.TextDecorations = null;
                    title.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
                }
                else
                {
                    title.TextDecorations = TextDecorations.Strikethrough;
                    title.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4");
                }
            }

            if (isCurEnable)
            {
                btnEnableSwitch.SvgIcon = "lucide/circle-minus";
                ToolTip.SetTip(btnEnableSwitch, Lang.Text("Setup.Java.Disable"));
            }
            else
            {
                btnEnableSwitch.SvgIcon = "lucide/circle-check";
                ToolTip.SetTip(btnEnableSwitch, Lang.Text("Setup.Java.Enable"));
            }
        }

        btnEnableSwitch.Click += (_, _) =>
        {
            try
            {
                if (!IsAvailable(j))
                {
                    HintService.Hint(Lang.Text("Setup.Java.Unavailable"));
                    return;
                }

                var enabled = IsJavaEnabled(j.Executable);
                if (enabled && PathComparer.Equals(Settings.DefaultJavaExecutable ?? "", j.Executable))
                {
                    HintService.Hint(Lang.Text("Setup.Java.DeselectBeforeDisable"));
                    return;
                }

                SetEnabled(j.Executable, !enabled);
                UpdateEnableStyle(!enabled);
                Settings.Save();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"切换 Java 启用状态失败：{ex}");
                Hint.Show(Lang.Text("Setup.Java.EnableFailed"), HintType.Error);
            }
        };
        UpdateEnableStyle(IsJavaEnabled(j.Executable));

        return item;
    }

    private static TextBlock? FindTitleLabel(MyListItem item) =>
        item.GetLogicalDescendants().OfType<TextBlock>().FirstOrDefault(text => text.Name == "LabTitle");

    private async void BtnAdd_Click(object? sender, RouteEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Lang.Text("Setup.Java.SelectFile.Title"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Lang.Text("Setup.Java.SelectFile.Filter"))
                {
                    Patterns = OperatingSystem.IsWindows() ? new[] { "java.exe", "javaw.exe" } : new[] { "java" }
                }
            ]
        });
        var ret = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (string.IsNullOrEmpty(ret) || !File.Exists(ret))
            return;
        if (ExistsInList(ret))
        {
            HintService.Hint(Lang.Text("Setup.Java.AlreadyExists"));
            return;
        }

        var added = await Task.Run(() =>
        {
            var runtime = ProbeJava(ret);
            if (runtime is null)
                return false;
            var settings = Settings;
            if (!settings.JavaManualPaths.Any(path => PathComparer.Equals(path, ret)))
                settings.JavaManualPaths.Add(ret);
            settings.Save();
            return true;
        });
        if (added)
        {
            Hint.Show(Lang.Text("Setup.Java.Added"), HintType.Success);
            loader.Start(true, true);
        }
        else
        {
            Hint.Show(Lang.Text("Setup.Java.AddFailed"), HintType.Error);
        }
    }

    #region 数据

    /// <summary>
    ///     启动页面是否应当使用 64 位的 Java。尚未扫描完成时视为 64 位。
    /// </summary>
    public static bool IsGameSet64BitJava()
    {
        var entries = GetEntries();
        if (entries is null)
        {
            EnsureScanned();
            return true;
        }

        var selected = Settings.DefaultJavaExecutable;
        if (!string.IsNullOrEmpty(selected))
        {
            var match = entries.FirstOrDefault(entry => PathComparer.Equals(entry.Executable, selected));
            if (match is not null)
                return match.Is64Bit;
        }

        var enabled = entries.Where(entry => IsJavaEnabled(entry.Executable) && IsAvailable(entry)).ToList();
        return enabled.Count == 0 || enabled.Any(entry => entry.Is64Bit);
    }

    /// <summary>
    ///     是否存在任何启用的 Java。尚未扫描完成时返回 false。
    /// </summary>
    public static bool ExistAnyJava()
    {
        var entries = GetEntries();
        if (entries is null)
        {
            EnsureScanned();
            return false;
        }

        return entries.Any(entry => IsJavaEnabled(entry.Executable) && IsAvailable(entry));
    }

    private static void EnsureScanned()
    {
        if (GetEntries() is not null)
            return;
        _ = Task.Run(() =>
        {
            if (GetEntries() is null)
                SetEntries(BuildEntries(JavaLocator.FindAll()));
        });
    }

    private static List<JavaEntry>? GetEntries()
    {
        lock (entryLock)
            return entryCache;
    }

    private static void SetEntries(List<JavaEntry> entries)
    {
        lock (entryLock)
            entryCache = entries;
    }

    private static List<JavaEntry> BuildEntries(IReadOnlyList<JavaRuntime> found)
    {
        var result = new List<JavaEntry>();
        var seen = new HashSet<string>(PathComparer);
        foreach (var runtime in found)
            if (seen.Add(runtime.Executable))
                result.Add(FromRuntime(runtime));
        foreach (var manual in Settings.JavaManualPaths)
        {
            if (seen.Contains(manual) || !File.Exists(manual))
                continue;
            var probed = ProbeJava(manual);
            if (probed is null)
                continue;
            seen.Add(manual);
            result.Add(FromRuntime(probed with { Executable = manual, Source = "Manual" }));
        }

        return result;
    }

    private static List<JavaEntry> SortEntries(IEnumerable<JavaEntry> entries) =>
        entries.OrderByDescending(entry => entry.MajorVersion).ThenBy(entry => entry.Executable, PathComparer)
            .ToList();

    private static JavaEntry FromRuntime(JavaRuntime runtime)
    {
        var binDir = Path.GetDirectoryName(runtime.Executable);
        var isJdk = binDir is not null &&
                    File.Exists(Path.Combine(binDir, OperatingSystem.IsWindows() ? "javac.exe" : "javac"));
        var javaFolder = binDir is null ? "" : Directory.GetParent(binDir)?.FullName ?? binDir;
        return new JavaEntry(runtime.Executable, runtime.MajorVersion, runtime.Version, runtime.Is64Bit,
            runtime.Source, isJdk, javaFolder);
    }

    private static JavaRuntime? ProbeJava(string path)
    {
        try
        {
            var startInfo = new ProcessStartInfo(path)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-version");
            using var process = Process.Start(startInfo);
            if (process is null)
                return null;
            var output = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(10000))
            {
                process.Kill();
                return null;
            }

            var info = JavaVersionParser.ParseVersionOutput(output);
            return info is null ? null : new JavaRuntime(path, info.MajorVersion, info.Version, info.Is64Bit, "Manual");
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static bool IsAvailable(JavaEntry entry) => File.Exists(entry.Executable);

    private static bool IsJavaEnabled(string executable) =>
        !Settings.JavaDisabledPaths.Any(path => PathComparer.Equals(path, executable));

    private static void SetEnabled(string executable, bool enabled)
    {
        var disabled = Settings.JavaDisabledPaths;
        disabled.RemoveAll(path => PathComparer.Equals(path, executable));
        if (!enabled)
            disabled.Add(executable);
    }

    private static bool ExistsInList(string executable)
    {
        var entries = GetEntries();
        return (entries?.Any(entry => PathComparer.Equals(entry.Executable, executable)) ?? false) ||
               Settings.JavaManualPaths.Any(path => PathComparer.Equals(path, executable));
    }

    #endregion
}
