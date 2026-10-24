using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PCL.Shion.Controls;
using PCL.Shion.Core.Export;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;
using Avalonia.Threading;

namespace PCL.Shion.Pages.Instance;

/// <summary>
///     导出页面中的一个选项：界面上的复选框对应的规则与默认状态。
/// </summary>
public class ExportOption
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>
    ///     导出该选项时包含的规则，多条以 | 分隔。
    /// </summary>
    public string? Rules { get; set; }

    /// <summary>
    ///     如果 Rules 为空，则根据 ShowRules 的内容判断是否应该显示这个复选框。
    ///     如果 ShowRules 也为空，则始终显示。
    /// </summary>
    public string? ShowRules { get; set; }

    public bool DefaultChecked { get; set; }
    public bool RequireModLoader { get; set; }
    public bool RequireOptiFine { get; set; }
    public bool RequireModLoaderOrOptiFine { get; set; }
}

public partial class PageInstanceExport : MyPageRight
{
    private string currentVersion = "";

    public PageInstanceExport()
    {
        InitializeComponent();
        PanScroll = PanBack;
        InitializeOptions();
        InitializeEvents();
        Loaded += (_, _) => PageInstanceExport_Loaded();
    }

    /// <summary>
    ///     创建每个复选框的导出选项。WPF 中这些选项写在 XAML 的 Tag 里，Avalonia 版在此处创建。
    /// </summary>
    private void InitializeOptions()
    {
        SetOption(CheckOptionsBasic, new ExportOption { Title = Lang.Text("Instance.Export.Option.Game"), DefaultChecked = true });
        SetOption(CheckOptionsOptions, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.GameSettings"),
            Description = Lang.Text("Instance.Export.Option.GameSettings.Desc"),
            Rules = "options.txt|configureddefaults/",
            DefaultChecked = true
        });
        SetOption(CheckOptionsGameProfile, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.GameProfile"),
            Description = Lang.Text("Instance.Export.Option.GameProfile.Desc"),
            Rules = "hotbar.nbt|command_history.txt",
            DefaultChecked = false
        });
        SetOption(CheckOptionsOptiFine, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.OptiFine"),
            Rules = "optionsof.txt|optionsshaders.txt",
            DefaultChecked = true,
            RequireOptiFine = true
        });
        SetOption(CheckOptionsMod, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.Mod"),
            Description = Lang.Text("Instance.Export.Option.Mod.Desc"),
            Rules = "mods/|!mods/*.disabled|!mods/*.old|!mods/.connector/|coremods/|lib/|!mods/mcef-libraries/|!mods/mcef-cache/",
            DefaultChecked = true,
            RequireModLoader = true
        });
        SetOption(CheckOptionsDisabledMod, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.DisabledMod"),
            Rules = "mods/*.disabled|mods/*.old"
        });
        SetOption(CheckOptionsImportantData, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.ImportantData"),
            Description = Lang.Text("Instance.Export.Option.ImportantData.Desc"),
            Rules = "addons/|multiblocked/|modpack-update-checker/|global_packs/|global_resource_packs/|global_data_packs/|optional_data_packs/|maps/|icon.png|mods-resourcepacks/|matmos/|resource_assorts/|resource_assorts.json|patchouli_books/|datapacks/|kubejs*/|!kubejs*/probe/|!kubejs*/exported/|!kubejs*/jsconfig.json|!kubejs*/README.txt|openloader/|worldshape/|resources/|scripts/|structures/|fontfiles/|oresources/|packmenu/|craftpresence/|pointblanks/|template*/|!template*/playerdata/|!template*/stats/",
            DefaultChecked = true
        });
        SetOption(CheckOptionsModSettings, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.ModSettings"),
            Rules = "config/|!config/accountsx/|!config/jei/world/|!config/worldedit/|config/worldedit/worldedit.properties|!config/spark/|config/spark/config.json|defaultconfigs/|journeymap/config/|journeymap/server/|TrashSlotSaveState.json|customfov.txt|gg.essential.mod/|essential/|!essential/*/|!essential/*.jar*|!essential/screenshot-checksum-caches.json|!essential/microsoft_accounts.json|paragliderSettings.nbt|local/client_config.json|local/ftbl.json|local/client/sidebar_buttons.json|local/client/ftbutilities.cfg|local/client/ftblib.cfg|local/client/xencraft.cfg|liteloader.properties|default_reference.xml|CustomSkinLoader/CustomSkinLoader.json",
            DefaultChecked = true
        });
        SetOption(CheckOptionsMaps, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.Maps"),
            Description = Lang.Text("Instance.Export.Option.Maps.Desc"),
            Rules = "journeymap/data/|xaero/|XaeroWaypoints/|XaeroWorldMap/",
            DefaultChecked = false
        });
        SetOption(CheckOptionsJei, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.Jei"),
            Description = Lang.Text("Instance.Export.Option.Jei.Desc"),
            Rules = "config/jei/world/",
            DefaultChecked = false
        });
        SetOption(CheckOptionsEmi, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.Emi"),
            Description = Lang.Text("Instance.Export.Option.Emi.Desc"),
            Rules = "emi.json",
            DefaultChecked = false
        });
        SetOption(CheckOptionsPatchouli, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.Patchouli"),
            Description = Lang.Text("Instance.Export.Option.Patchouli.Desc"),
            Rules = "patchouli_data.json",
            DefaultChecked = false
        });
        SetOption(CheckOptionsResourcePacks, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.ResourcePacks"),
            Description = Lang.Text("Instance.Export.Option.ResourcePacks.Desc"),
            ShowRules = "resourcepacks/|texturepacks/",
            DefaultChecked = true
        });
        SetOption(CheckOptionsShaderPacks, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.ShaderPacks"),
            ShowRules = "shaderpacks/",
            DefaultChecked = true,
            RequireModLoaderOrOptiFine = true
        });
        SetOption(CheckOptionsScreenshots, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.Screenshots"),
            Rules = "screenshots/",
            DefaultChecked = false
        });
        SetOption(CheckOptionsStructures, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.Structures"),
            Description = Lang.Text("Instance.Export.Option.Structures.Desc"),
            Rules = "schematics/",
            DefaultChecked = false
        });
        SetOption(CheckOptionsReplay, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.Replay"),
            Description = Lang.Text("Instance.Export.Option.Replay.Desc"),
            Rules = "replay_recordings/|replay_videos/",
            RequireModLoader = true,
            DefaultChecked = false
        });
        SetOption(CheckOptionsSaves, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.Saves"),
            Description = Lang.Text("Instance.Export.Option.Saves.Desc"),
            ShowRules = "saves/",
            DefaultChecked = false
        });
        SetOption(CheckOptionsServers, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.Servers"),
            Rules = "servers.dat",
            DefaultChecked = false
        });
        SetOption(CheckOptionsOtherFolders, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.OtherFolders"),
            Description = Lang.Text("Instance.Export.Option.OtherFolders.Desc"),
            DefaultChecked = false
        });
        SetOption(CheckOptionsPcl, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.Launcher"),
            Description = Lang.Text("Instance.Export.Option.Launcher.Desc"),
            DefaultChecked = false
        });
        SetOption(CheckOptionsPclCustom, new ExportOption
        {
            Title = Lang.Text("Instance.Export.Option.LauncherCustom"),
            Description = Lang.Text("Instance.Export.Option.LauncherCustom.Desc"),
            DefaultChecked = true
        });
        SetOption(CheckAdvancedInclude, null);
        SetOption(CheckAdvancedModrinth, null);
    }

    private static void SetOption(MyCheckBox checkBox, ExportOption? option)
    {
        if (option is not null)
            checkBox.Tag = option;
    }

    /// <summary>
    ///     WPF 中的 XAML 事件特性在 Avalonia 中改为代码绑定，并把子面板的可见性绑定到对应复选框。
    /// </summary>
    private void InitializeEvents()
    {
        BtnAdvancedExport.Click += ExportConfig;
        BtnAdvancedImport.Click += ImportConfig;
        BtnExport.Click += StartExport;
        CardOptions.PointerPressed += CardOptions_PointerPressed;
        TextExportName.GotFocus += TextExportName_GotFocus;
        CheckAdvancedModrinth.Change += CheckAdvancedModrinth_Change;
        CheckAdvancedInclude.Change += CheckAdvancedInclude_Change;
        CheckOptionsOtherFolders.Change += CheckOptionsOtherFolders_Change;
        BindVisibility(CheckOptionsMod, PanOptionsMod);
        BindVisibility(CheckOptionsResourcePacks, PanOptionsResourcePacks);
        BindVisibility(CheckOptionsShaderPacks, PanOptionsShaderPacks);
        BindVisibility(CheckOptionsSaves, PanOptionsSaves);
        BindVisibility(CheckOptionsOtherFolders, PanOptionsOtherFolders);
        BindVisibility(CheckOptionsPcl, PanOptionsPcl);
        BindVisibility(CheckAdvancedInclude, HintBundle);
        AddHandler(DragDrop.DragEnterEvent, PanAllBack_DragEnter);
        AddHandler(DragDrop.DropEvent, PanAllBack_Drop);
    }

    /// <summary>
    ///     WPF 中 Visibility 绑定到 CheckBox 的 Checked，Avalonia 版在 Checked 改变时同步。
    /// </summary>
    private static void BindVisibility(MyCheckBox source, Control target)
    {
        target.IsVisible = source.Checked == true;
        source.Change += (_, _) => target.IsVisible = source.Checked == true;
    }

    private void PageInstanceExport_Loaded()
    {
        ModAnimation.AniControlEnabled += 1;
        if ((currentVersion ?? "") != (PageInstanceLeft.McInstance.PathInstance ?? ""))
            RefreshAll(); // 切换到了另一个实例，重置页面
        ModAnimation.AniControlEnabled -= 1;
    }

    public void RefreshAll()
    {
        Log("[Export] 刷新导出页面");
        HintOptiFine.IsVisible = PageInstanceLeft.McInstance.Info.HasOptiFine;
        currentVersion = PageInstanceLeft.McInstance.PathInstance;
        TextExportName.Text = "";
        TextExportName.HintText = PageInstanceLeft.McInstance.Name;
        TextExportVersion.Text = "";
        TextExportVersion.HintText = "1.0.0";
        CheckAdvancedInclude.Checked = false;
        CheckAdvancedModrinth.Checked = false;
        GetExportOption(CheckOptionsBasic).Description = PageInstanceLeft.McInstance.GetDefaultDescription();
        ResetConfigOverrides();
        ReloadAllSubOptions();
        RefreshAllOptionsUI();
        PanBack.ScrollToHome();
    }

    // 自动填写整合包名称
    private void TextExportName_GotFocus(object? sender, RoutedEventArgs routedEventArgs)
    {
        if (string.IsNullOrEmpty(TextExportName.Text))
        {
            TextExportName.Text = TextExportName.HintText;
            TextExportName.SelectionStart = TextExportName.Text?.Length ?? 0;
        }
    }

    // 勾选 Modrinth 上传模式时，禁止打包 PCL
    private void CheckAdvancedModrinth_Change(object? sender, bool user)
    {
        if (CheckAdvancedModrinth.Checked == true)
            CheckOptionsPcl.Checked = false;
        CheckOptionsPcl.IsEnabled = CheckAdvancedModrinth.Checked != true;
    }

    // 勾选"其他文件夹"时，同步勾选/取消所有子选项
    private void CheckOptionsOtherFolders_Change(object? sender, bool user)
    {
        if (!user) return;
        foreach (var child in PanOptionsOtherFolders.Children)
            if (child is MyCheckBox childBox)
                childBox.Checked = CheckOptionsOtherFolders.Checked;
    }

    // 勾选打包资源文件时，禁止开启 Modrinth 上传模式
    private void CheckAdvancedInclude_Change(object? sender, bool user)
    {
        if (CheckAdvancedInclude.Checked == true)
            CheckAdvancedModrinth.Checked = false;
        CheckAdvancedModrinth.IsEnabled = CheckAdvancedInclude.Checked != true;
    }

    private static void Log(string message) => ModBase.Log(message);

    #region 子选项

    private readonly string[] subOptionBlackList = ["Quark Programmer Art.zip", "+ EuphoriaPatches_"];

    /// <summary>
    ///     动态生成子文件夹下的选项，例如资源包、存档等。
    /// </summary>
    private void ReloadAllSubOptions()
    {
        ReloadSubOptions(PanOptionsResourcePacks, true, true, "resourcepacks", "texturepacks");
        ReloadSubOptions(PanOptionsSaves, false, true, "saves");
        ReloadSubOptions(PanOptionsShaderPacks, true, true, "shaderpacks");
        ReloadOtherFolders();
    }

    /// <summary>
    ///     扫描实例根目录下未被已有选项覆盖的文件夹，生成独立的复选框。
    /// </summary>
    private void ReloadOtherFolders()
    {
        PanOptionsOtherFolders.Children.Clear();

        var pathIndie = PageInstanceLeft.McInstance.PathIndie;
        var rootDir = new DirectoryInfo(pathIndie);
        if (!rootDir.Exists)
        {
            CheckOptionsOtherFolders.IsVisible = false;
            return;
        }

        var coveredFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 模组
            "mods", "coremods", "lib",
            // 整合包重要数据
            "addons", "multiblocked", "modpack-update-checker", "global_packs",
            "global_resource_packs", "global_data_packs", "optional_data_packs", "maps",
            "mods-resourcepacks", "matmos", "resource_assorts",
            "patchouli_books", "datapacks",
            "openloader", "worldshape", "resources", "scripts", "structures",
            "fontfiles", "oresources", "packmenu", "craftpresence", "pointblanks",
            // 模组设置
            "config", "defaultconfigs", "journeymap", "local", "essential", "gg.essential.mod",
            "CustomSkinLoader",
            // 地图
            "xaero", "XaeroWaypoints", "XaeroWorldMap",
            // 资源包
            "resourcepacks", "texturepacks",
            // 光影
            "shaderpacks",
            // 截图 / 结构 / 录像
            "screenshots", "schematics",
            "replay_recordings", "replay_videos",
            // 存档 / 设置文件夹
            "saves", "configureddefaults",
            // 始终跳过（大量文件或无用缓存）
            "assets", "versions", "libraries", "structureCacheV1",
            ".fabric", ".git", "avatar-cache", "cosmetic-cache",
            // PCL 单独处理
            "PCL",
        };

        var coveredPrefixes = new[] { "kubejs", "template" };
        var coveredSuffixes = new[] { "-natives" };

        foreach (var subDir in rootDir.EnumerateDirectories())
        {
            if (coveredFolders.Contains(subDir.Name))
                continue;
            if (coveredPrefixes.Any(p => subDir.Name.StartsWith(p, StringComparison.Ordinal)))
                continue;
            if (coveredSuffixes.Any(s => subDir.Name.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
                continue;

            PanOptionsOtherFolders.Children.Add(new MyCheckBox
            {
                Tag = new ExportOption
                {
                    Title = subDir.Name,
                    DefaultChecked = false,
                    Rules = ExportRules.EscapeLikePattern($"{subDir.Name}/")
                }
            });
        }

        CheckOptionsOtherFolders.IsVisible = PanOptionsOtherFolders.Children.Count > 0;
    }

    private void ReloadSubOptions(StackPanel panel, bool acceptCompressedFile, bool acceptFolder,
        params string[] folders)
    {
        panel.Children.Clear();
        foreach (var folder in folders)
        {
            var targetFolder = new DirectoryInfo(PageInstanceLeft.McInstance.PathIndie + folder);
            if (!targetFolder.Exists)
                continue;
            // 查找文件夹下的对应项
            if (acceptCompressedFile)
                foreach (var file in targetFolder.EnumerateFiles("*.zip").Concat(targetFolder.EnumerateFiles("*.rar")))
                {
                    if (subOptionBlackList.Any(b => file.Name.Contains(b, StringComparison.Ordinal)))
                        continue;
                    panel.Children.Add(new MyCheckBox
                    {
                        Tag = new ExportOption
                        {
                            Title = file.Name, DefaultChecked = true,
                            Rules = ExportRules.EscapeLikePattern($"{folder}/{file.Name}")
                        }
                    });
                    if (folder == "shaderpacks") // 处理光影包的配置文件
                    {
                        var shaderConfig = new FileInfo(Path.Combine(file.Directory!.FullName,
                            $"{file.Name}.txt"));
                        if (shaderConfig.Exists)
                            panel.Children.Add(new MyCheckBox
                            {
                                Margin = new Thickness(30, 0, 0, 0),
                                Tag = new ExportOption
                                {
                                    Title = $"{shaderConfig.Name}", DefaultChecked = true,
                                    Description = Lang.Text("Instance.Export.Config.ShaderConfigSuffix"),
                                    Rules = ExportRules.EscapeLikePattern($"{folder}/{shaderConfig.Name}")
                                }
                            });
                    }
                }

            if (acceptFolder)
                foreach (var subFolder in targetFolder.EnumerateDirectories().OrderByDescending(f => f.LastWriteTime))
                {
                    if (subOptionBlackList.Any(b => subFolder.Name.Contains(b, StringComparison.Ordinal)))
                        continue;
                    if (!subFolder.EnumerateFileSystemInfos().Any())
                        continue;
                    var newCheckBox = new MyCheckBox
                    {
                        Tag = new ExportOption
                        {
                            Title = subFolder.Name, DefaultChecked = true,
                            Rules = ExportRules.EscapeLikePattern($"{folder}/{subFolder.Name}/")
                        }
                    };
                    if (ReferenceEquals(panel, PanOptionsSaves))
                        GetExportOption(newCheckBox).Description = Lang.Date(subFolder.LastWriteTime, "g");
                    panel.Children.Add(newCheckBox);
                    if (folder == "shaderpacks") // 处理文件夹形式光影包的配置文件
                    {
                        var shaderConfig = new FileInfo(Path.Combine(targetFolder.FullName,
                            $"{subFolder.Name}.txt"));
                        if (shaderConfig.Exists)
                            panel.Children.Add(new MyCheckBox
                            {
                                Margin = new Thickness(30, 0, 0, 0),
                                Tag = new ExportOption
                                {
                                    Title = $"{shaderConfig.Name}", DefaultChecked = true,
                                    Description = Lang.Text("Instance.Export.Config.ShaderConfigSuffix"),
                                    Rules = ExportRules.EscapeLikePattern($"{folder}/{shaderConfig.Name}")
                                }
                            });
                    }
                }
        }
    }

    #endregion

    #region 选项

    /// <summary>
    ///     重新确认是否应该显示每个选项，并将 ExportOption 同步到 UI。
    /// </summary>
    private void RefreshAllOptionsUI()
    {
        // 预先归纳所有至多二级的文件/文件夹
        var allEntries = new List<string>();

        bool IsValidDirectory(DirectoryInfo folder)
        {
            try
            {
                return folder.Exists && folder.EnumerateFileSystemInfos()
                    .Any(i => !subOptionBlackList.Any(b => i.Name.Contains(b, StringComparison.Ordinal)));
            }
            catch
            {
                return false;
            }
        }

        // 检查文件夹不为空
        // 一般是由于无法访问，或是一个指向已不存在的文件夹的链接（例如使用 mklink 创造的 resource 文件夹链接）
        var pathInfo = new DirectoryInfo(PageInstanceLeft.McInstance.PathIndie);
        allEntries.AddRange(pathInfo.EnumerateFiles().Select(f => f.Name));
        foreach (var subFolder in pathInfo.EnumerateDirectories().Where(IsValidDirectory))
        {
            allEntries.Add($"{subFolder.Name}/");
            allEntries.AddRange(subFolder.EnumerateFiles().Select(f => $"{subFolder.Name}/{f.Name}"));
            allEntries.AddRange(subFolder.EnumerateDirectories().Where(IsValidDirectory)
                .Select(d => $"{subFolder.Name}/{d.Name}/"));
        }

        Log($"[Export] 共发现 {allEntries.Count} 个可行的二级文件/文件夹");

        // 确认选项是否应该被显示
        bool IsVisible(ExportOption targetOption)
        {
            // 检查需要 OptiFine 或 Mod 加载器
            if (targetOption.RequireOptiFine && !PageInstanceLeft.McInstance.Info.HasOptiFine)
                return false;
            if (targetOption.RequireModLoader && !PageInstanceLeft.McInstance.Modable)
                return false;
            if (targetOption.RequireModLoaderOrOptiFine && !PageInstanceLeft.McInstance.Info.HasOptiFine &&
                !PageInstanceLeft.McInstance.Modable)
                return false;
            // 粗略检查是否可能有符合规则的文件/文件夹
            return ExportRules.Standardize((targetOption.Rules ?? targetOption.ShowRules ?? "").Split('|'), true).Any(rule =>
            {
                if (rule.StartsWith('!'))
                    return false; // 只看正向规则
                // 检查前两级
                try
                {
                    if (allEntries.Any(entry => ExportRules.Matches(entry, rule)))
                        return true;
                }
                catch (Exception ex)
                {
                    ModBase.Log(
                        ex,
                        $"错误的规则：{rule}",
                        ModBase.LogLevel.Hint,
                        userSummary: Lang.Text("Instance.Export.Error.OperationFailed"));
                    return false;
                }

                // 粗略检查所有级
                rule = rule.Trim('*', '?');
                if (rule.Split('/', StringSplitOptions.RemoveEmptyEntries).Length >= 3)
                {
                    if (rule.EndsWith('/'))
                        return IsValidDirectory(new DirectoryInfo(PageInstanceLeft.McInstance.PathIndie + rule)); // 文件夹有效

                    return File.Exists(PageInstanceLeft.McInstance.PathIndie + rule); // 文件有效
                }

                return false;
            });
        }

        // 逐个检查选项
        foreach (var checkBox in GetAllOptions(true))
        {
            var targetOption = GetExportOption(checkBox);
            // 名称与简介
            checkBox.Inlines!.Clear();
            checkBox.Inlines.Add(new Run(targetOption.Title));
            if (!string.IsNullOrEmpty(targetOption.Description))
                checkBox.Inlines.Add(new Run("   " + targetOption.Description) { Foreground = Brush("ColorBrushGray5") });
            // 可见性、默认勾选
            if (string.IsNullOrEmpty(targetOption.Rules) && string.IsNullOrEmpty(targetOption.ShowRules))
            {
                checkBox.IsVisible = true;
                checkBox.Checked = targetOption.DefaultChecked;
            }
            else
            {
                var pass = IsVisible(targetOption);
                checkBox.IsVisible = pass;
                checkBox.Checked = targetOption.DefaultChecked && pass;
            }
        }
    }

    private IBrush Brush(string key) => this.FindBrushOrTransparent(key);

    /// <summary>
    ///     获取所有可作为选项的 CheckBox。
    /// </summary>
    private IEnumerable<MyCheckBox> GetAllOptions(bool includeHidden)
    {
        foreach (var element in PanOptions.Children)
        {
            if (!includeHidden && !element.IsVisible)
                continue;
            if (element is MyCheckBox checkBox)
                yield return checkBox;
            else if (element is StackPanel stack)
                foreach (var subElement in stack.Children)
                {
                    if (!includeHidden && !subElement.IsVisible)
                        continue;
                    if (subElement is MyCheckBox subCheckBox)
                        yield return subCheckBox;
                }
        }
    }

    /// <summary>
    ///     获取该 CheckBox 对应的 ExportOption。
    /// </summary>
    private static ExportOption GetExportOption(MyCheckBox checkBox)
    {
        return (ExportOption)checkBox.Tag!;
    }

    #endregion

    #region 配置文件

    // ================ 导出内容段 ================

    /// <summary>
    ///     从配置文件中读取的规则。
    ///     如果不为 Nothing，则会覆写当前勾选的规则并禁用对应 UI。
    /// </summary>
    private List<string>? _rulesOverrides;

    private List<string>? RulesOverrides
    {
        get => _rulesOverrides;
        set
        {
            _rulesOverrides = value;
            CardOptions.Inlines!.Clear();
            if (value is null)
            {
                BtnOverrideCancel.IsVisible = false;
                PanOptions.IsVisible = true;
                CardOptions.Inlines.Add(new Run(Lang.Text("Instance.Export.OptionListTitle")) { FontWeight = FontWeight.Bold });
            }
            else
            {
                BtnOverrideCancel.IsVisible = true;
                PanOptions.IsVisible = false;
                CardOptions.Inlines.Add(new Run(Lang.Text("Instance.Export.OptionListTitle") + ":    ") { FontWeight = FontWeight.Bold });
                CardOptions.Inlines.Add(new Run(Lang.Text("Instance.Export.OptionList.FromConfig")) { FontWeight = FontWeight.Normal });
            }
        }
    }

    /// <summary>
    ///     获取当前实际生效的所有规则。
    /// </summary>
    private IEnumerable<string> GetAllRules()
    {
        if (RulesOverrides is not null)
        {
            // 返回覆盖的列表
            foreach (var rule in RulesOverrides)
                yield return rule;
        }
        else
        {
            // 从当前勾选的所有选项中获取所有规则行
            yield return "";
            yield return "# " + Lang.Text("Instance.Export.Config.Comment.ModifyRules");
            yield return "# " + Lang.Text("Instance.Export.Config.Comment.ReverseMatch");
            yield return "";
            foreach (var checkBox in GetAllOptions(false))
            {
                if (checkBox.Checked != true)
                    continue;
                var targetOption = GetExportOption(checkBox);
                if (targetOption.Rules is null)
                    continue;
                yield return $"# {targetOption.Title}";
                foreach (var rule in targetOption.Rules.Split('|'))
                    yield return rule;
                yield return "";
            }

            yield return "# " + Lang.Text("Instance.Export.Config.Comment.ExcludedFiles");
            yield return "!*.log";
            yield return "!*.dat_old";
            yield return "!*.BakaCoreInfo";
            yield return "!hmclversion.cfg";
            yield return "!log4j2.xml";
            yield return "";
        }
    }

    // ================ 追加内容段 ================

    private List<string>? extraFiles;

    /// <summary>
    ///     获取当前实际生效的追加内容。
    /// </summary>
    private IEnumerable<string> GetExtraFileLines()
    {
        if (extraFiles is not null)
        {
            // 返回覆盖的列表
            foreach (var file in extraFiles)
                yield return file;
        }
        else
        {
            // 从当前勾选的所有选项中获取所有规则行
            yield return "";
            yield return "# " + Lang.Text("Instance.Export.Config.Comment.ExtraFiles");
            yield return "# " + Lang.Text("Instance.Export.Config.Comment.ExtraFiles2");
            yield return "";
        }
    }

    // ================ 重置 ================

    /// <summary>
    ///     重置配置文件所带来的影响。
    /// </summary>
    private void ResetConfigOverrides()
    {
        RulesOverrides = null;
        configPackPath = null;
        extraFiles = null;
        PanBack.ScrollToHome();
    }

    private void CardOptions_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (RulesOverrides is null)
            return;
        ResetConfigOverrides();
    }

    // ================ 保存 / 读取 ================

    // 保存配置文件
    private async void ExportConfig(object? sender, RoutedEventArgs e)
    {
        try
        {
            var configPath = await SelectSaveFileAsync(Lang.Text("Instance.Export.SelectFileLocation"), "export_config.txt",
                Lang.Text("Instance.Export.Config.FileFilter"));
            if (string.IsNullOrEmpty(configPath))
                return;
            App.Services.Settings.ExportConfigPath = configPath;
            var configLines = new List<string>();
            // ini 段
            configLines.Add("Name:" + TextExportName.Text);
            configLines.Add("Version:" + TextExportVersion.Text);
            configLines.Add("");
            configLines.Add("# " + Lang.Text("Instance.Export.Config.Comment.IncludeLauncher"));
            configLines.Add("IncludeLauncher:" + CheckOptionsPcl.Checked);
            configLines.Add("");
            configLines.Add("# " + Lang.Text("Instance.Export.Config.Comment.IncludeLauncherCustom"));
            configLines.Add("IncludeLauncherCustom:" + CheckOptionsPclCustom.Checked);
            configLines.Add("");
            configLines.Add("# " + Lang.Text("Instance.Export.Config.Comment.BundleFiles"));
            configLines.Add("# " + Lang.Text("Instance.Export.Config.Comment.BundleFiles2"));
            configLines.Add("# " + Lang.Text("Instance.Export.Config.Comment.BundleFiles3"));
            configLines.Add("DontCheckHostedAssets:" + CheckAdvancedInclude.Checked);
            configLines.Add("");
            configLines.Add("# " + Lang.Text("Instance.Export.Config.Comment.Modrinth"));
            configLines.Add("# " + Lang.Text("Instance.Export.Config.Comment.Modrinth2"));
            configLines.Add("# " + Lang.Text("Instance.Export.Config.Comment.Modrinth3"));
            configLines.Add("ModrinthUploadMode:" + CheckAdvancedModrinth.Checked);
            configLines.Add("");
            configLines.Add("# " + Lang.Text("Instance.Export.Config.Comment.PackPath"));
            configLines.Add("# " + Lang.Text("Instance.Export.Config.Comment.PackPath2"));
            configLines.Add("# " + Lang.Text("Instance.Export.Config.Comment.PackPath3"));
            configLines.Add("PackPath:" + (configPackPath ?? ""));
            configLines.Add("");
            // 导出内容段
            configLines.Add(ExportConfigSeparator);
            configLines.AddRange(GetAllRules());
            // 追加内容段
            configLines.Add(ExportConfigSeparator);
            configLines.AddRange(GetExtraFileLines());
            // 结束
            File.WriteAllText(configPath, string.Join("\r\n", configLines));
            Hint.Show(Lang.Text("Instance.Export.SaveSuccess", configPath), HintType.Success);
            OpenContainingFolder(configPath);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "保存配置失败",
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Export.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     段与段之间的分隔行。
    /// </summary>
    private const string ExportConfigSeparator = PCL.Shion.Core.Export.ExportConfig.Separator;

    #region 配置文件核心读取逻辑

    /// <summary>
    ///     从指定路径读取配置文件（供按钮和拖放调用）
    /// </summary>
    /// <param name="configPath">配置文件路径</param>
    private void ReadConfigFile(string configPath)
    {
        try
        {
            // 保存配置文件路径到缓存
            App.Services.Settings.ExportConfigPath = configPath;

            var config = PCL.Shion.Core.Export.ExportConfig.Parse(File.ReadAllText(configPath));

            // 赋值到界面控件
            TextExportName.Text = config.Name;
            TextExportVersion.Text = config.Version;
            CheckOptionsPcl.Checked = config.IncludeLauncher;
            CheckOptionsPclCustom.Checked = config.IncludeLauncherCustom;
            CheckAdvancedModrinth.Checked = config.ModrinthUploadMode;
            CheckAdvancedInclude.Checked = config.DontCheckHostedAssets;
            configPackPath = config.PackPath;

            // === 解析导出内容段 ===
            RulesOverrides = config.RulesOverrides;

            // === 解析追加内容段 ===
            extraFiles = config.ExtraFiles;

            // 提示成功
            Hint.Show(Lang.Text("Instance.Export.ReadSuccess", configPath), HintType.Success);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                $"读取配置文件失败：{configPath}",
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Export.Error.OperationFailed"));
        }
    }

    #endregion

    // 读取配置文件
    private async void ImportConfig(object? sender, RoutedEventArgs e)
    {
        try
        {
            var configPath = await SelectOpenFileAsync(Lang.Text("Instance.Export.Config.FileFilter"),
                Lang.Text("Instance.Export.SelectConfigFile"));
            if (string.IsNullOrEmpty(configPath))
                return;

            // 调用核心读取逻辑
            ReadConfigFile(configPath);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "选择配置文件失败",
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Export.Error.OperationFailed"));
        }
    }

    #region 拖放事件处理

    /// <summary>
    ///     文件拖入界面时触发：验证文件类型
    /// </summary>
    private void PanAllBack_DragEnter(object? sender, DragEventArgs e)
    {
        var files = GetDroppedFiles(e);
        // 验证：仅允许单个.txt文件
        e.DragEffects = files is { Length: 1 } && files[0].EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    ///     文件放下时触发：读取配置文件
    /// </summary>
    private void PanAllBack_Drop(object? sender, DragEventArgs e)
    {
        var files = GetDroppedFiles(e);
        if (files is { Length: > 0 })
            ReadConfigFile(files[0]);
        e.Handled = true;
    }

    private static string[]? GetDroppedFiles(DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
            return null;
        return files.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
    }

    #endregion

    #endregion

    #region 导出

    /// <summary>
    ///     配置文件中指定的导出位置。
    /// </summary>
    private string? configPackPath;

    /// <summary>
    ///     开始导出。
    /// </summary>
    private async void StartExport(object? sender, RoutedEventArgs e)
    {
        var packName = string.IsNullOrEmpty(TextExportName.Text) ? TextExportName.HintText : TextExportName.Text;
        var packVersion = string.IsNullOrEmpty(TextExportVersion.Text) ? "1.0.0" : TextExportVersion.Text;

        // 重复任务检查
        var loaderName = Lang.Text("Instance.Export.ExportTask.Prefix") + packName;
        foreach (var ongoingLoader in ModLoader.loaderTaskbar)
        {
            if ((ongoingLoader.name ?? "") != (loaderName ?? ""))
                continue;
            Navigator.Go(PageType.TaskManager);
            return;
        }

        // 确认导出位置
        string? packPath = null;
        if (!string.IsNullOrWhiteSpace(configPackPath) && !configPackPath.EndsWith('\\') &&
            !configPackPath.EndsWith('/'))
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPackPath)!);
                packPath = configPackPath;
                Log($"[Export] 使用配置文件中指定的导出路径：{configPackPath}");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"无法使用配置文件中指定的导出路径（{configPackPath}）");
                if (await MsgBox.ShowAsync(
                        Lang.Text("Instance.Export.PackPathInvalid.WithDetail", configPackPath, ex.ToString()),
                        Lang.Text("Instance.Export.PackPathInvalid.Title"), Lang.Text("Common.Action.Confirm"),
                        Lang.Text("Common.Action.Cancel")) == 2)
                    return;
            }

        if (packPath is null)
        {
            var extensions = new List<string>();
            if (CheckAdvancedModrinth.Checked == false)
                extensions.Add(Lang.Text("Instance.Export.ZipFilter"));
            if (CheckOptionsPcl.Checked == false)
                extensions.Add(Lang.Text("Instance.Export.MrpackFilter"));
            packPath = await SelectSaveFileAsync(Lang.Text("Instance.Export.SelectSaveLocation"),
                packName + (string.IsNullOrEmpty(TextExportVersion.Text) ? "" : " " + TextExportVersion.Text),
                string.Join("|", extensions));
            Log($"[Export] 手动指定的导出路径：{packPath}");
        }

        if (string.IsNullOrEmpty(packPath))
            return;

        // 缓存所需参数
        var cacheFolder = Path.Combine(Path.GetTempPath(), "PCL Shion", "export-" + Guid.NewGuid().ToString("N"));
        var overridesFolder = Path.Combine(cacheFolder, "modpack", "overrides");
        var mcInstance = PageInstanceLeft.McInstance;
        var pathIndie = mcInstance.PathIndie;
        var checkHostedAssets = CheckAdvancedInclude.Checked != true;
        var modrinthUploadMode = CheckAdvancedModrinth.Checked == true;
        var includePCL = CheckOptionsPcl.Checked == true;
        var includePCLCustom = includePCL && CheckOptionsPclCustom.Checked == true;
        var allRules = ExportRules.Standardize(GetAllRules(), true).ToList();
        var allExtraFiles = ExportRules.Standardize(GetExtraFileLines(), false).ToList();
        Log($"[Export] 准备导出整合包，共有 {allRules.Count} 条规则，{allExtraFiles.Count} 条追加内容行");

        // 构造步骤加载器
        var loaders = new List<ModLoader.LoaderBase>();
        var hostedFiles = new List<ExportFile>();
        var downloadUrls = new Dictionary<ExportFile, List<string>>();

        #region 复制文件

        loaders.Add(new ModLoader.LoaderTask<int, int>(
            Lang.Text("Instance.Export.Task.CopyContent"), loader =>
            {
                // 复制实例文件
                hostedFiles = ModpackExporter.CopyOverrides(pathIndie, overridesFolder, allRules, checkHostedAssets,
                    new Progress<double>(processed => loader.Progress = Math.Min(0.94d, processed / 10000d)));
                Log($"[Export] 复制 overrides 文件完成，有 {hostedFiles.Count} 个文件需要联网检查");
                loader.Progress = 0.95d;
                // 复制追加内容到根目录
                var baseFolder = includePCL ? cacheFolder : Path.Combine(cacheFolder, "modpack");
                foreach (var line in allExtraFiles)
                    if (line.EndsWith('\\') || line.EndsWith('/'))
                    {
                        if (Directory.Exists(line))
                            ModpackExporter.CopyDirectory(line, Path.Combine(baseFolder, Path.GetFileName(line.TrimEnd('\\', '/'))));
                        else
                            Hint.Show(Lang.Text("Instance.Export.Config.FolderNotFound", line), HintType.Error);
                    }
                    else if (File.Exists(line))
                    {
                        File.Copy(line, Path.Combine(baseFolder, Path.GetFileName(line)), true);
                    }
                    else
                    {
                        Hint.Show(Lang.Text("Instance.Export.Config.FileNotFound", line), HintType.Error);
                    }

                loader.Progress = 0.97d;
                // 复制 PCL 实例设置
                ModpackExporter.CopyDirectory(Path.Combine(mcInstance.PathInstance, "PCL"), Path.Combine(overridesFolder, "PCL"));
                // 复制 PCL 本体
                if (includePCL && Environment.ProcessPath is { } exePath)
                    File.Copy(exePath, Path.Combine(cacheFolder, Path.GetFileName(exePath)), true);
                // 复制 PCL 个性化内容
                if (includePCLCustom)
                {
                    var exeFolder = AppContext.BaseDirectory;
                    ModpackExporter.CopyDirectory(Path.Combine(exeFolder, "PCL", "Pictures"), Path.Combine(cacheFolder, "PCL", "Pictures"));
                    ModpackExporter.CopyDirectory(Path.Combine(exeFolder, "PCL", "Musics"), Path.Combine(cacheFolder, "PCL", "Musics"));
                    CopyIfExists(Path.Combine(exeFolder, "PCL", "Custom.xaml"), Path.Combine(cacheFolder, "PCL", "Custom.xaml"));
                    CopyIfExists(Path.Combine(exeFolder, "PCL", "Setup.ini"), Path.Combine(cacheFolder, "PCL", "Setup.ini"));
                    CopyIfExists(Path.Combine(exeFolder, "PCL", "hints.txt"), Path.Combine(cacheFolder, "PCL", "hints.txt"));
                    CopyIfExists(Path.Combine(exeFolder, "PCL", "Logo.png"), Path.Combine(cacheFolder, "PCL", "Logo.png"));
                }
            })
        {
            ProgressWeight = 5d
        });

        #endregion

        #region 联网检查

        loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Instance.Export.Task.FetchFileInfo"), loader =>
            {
                if (!checkHostedAssets)
                {
                    Log("[Export] 要求跳过联网获取步骤");
                    return;
                }

                if (hostedFiles.Count == 0)
                {
                    Log("[Export] 没有需要联网检查的文件，跳过联网获取步骤");
                    return;
                }

                var result = ModpackExporter.LookupDownloadUrlsAsync(App.Services.Engine.Http, hostedFiles,
                        modrinthUploadMode, loader.AbortedToken)
                    .GetAwaiter().GetResult();
                foreach (var pair in result.Urls)
                    downloadUrls[pair.Key] = pair.Value;
                loader.Progress = 0.9d;

                // 若失败，确认是否继续
                if (result.Failures.Count == 1)
                {
                    if (AskOnUiThread(Lang.Text("Instance.Export.NetCheckPartialFailed.Message"),
                            Lang.Text("Instance.Export.NetCheckPartialFailed.Title")) == 2)
                        throw result.Failures.First();
                }
                else if (result.Failures.Count > 1)
                {
                    if (AskOnUiThread(Lang.Text("Instance.Export.NetCheckAllFailed.Message"),
                            Lang.Text("Instance.Export.NetCheckAllFailed.Title")) == 2)
                        throw result.Failures.First();
                }
            })
        {
            show = checkHostedAssets,
            ProgressWeight = checkHostedAssets ? 2d : 0.01d
        });

        #endregion

        #region 生成压缩包

        loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Instance.Export.Task.CreateArchive"), loader =>
        {
            // 整理文件列表并生成 modrinth.index.json
            var index = ModpackExporter.BuildIndex(overridesFolder, downloadUrls,
                mcInstance.Info.VanillaName, mcInstance.Info.HasForge ? mcInstance.Info.Forge : null,
                mcInstance.Info.HasFabric ? mcInstance.Info.Fabric : null,
                mcInstance.Info.HasNeoForge ? mcInstance.Info.NeoForge : null,
                packVersion, packName, mcInstance.GetDefaultDescription());
            foreach (var file in downloadUrls.Keys)
                File.Delete(file.Path);
            loader.Progress = 0.2d;
            File.WriteAllText(Path.Combine(cacheFolder, "modpack", "modrinth.index.json"), index);
            // 打包
            Directory.CreateDirectory(Path.GetDirectoryName(packPath)!);
            if (File.Exists(packPath))
                File.Delete(packPath);
            if (includePCL)
            {
                // 首次压缩整合包
                ZipFile.CreateFromDirectory(Path.Combine(cacheFolder, "modpack"), Path.Combine(cacheFolder, "modpack.mrpack"));
                loader.Progress = 0.5d;
                Directory.Delete(Path.Combine(cacheFolder, "modpack"), true);
                loader.Progress = 0.6d;
                // 二次压缩整合包
                ZipFile.CreateFromDirectory(cacheFolder, packPath);
                loader.Progress = 0.9d;
            }
            else
            {
                // 直接压缩整合包
                ZipFile.CreateFromDirectory(Path.Combine(cacheFolder, "modpack"), packPath);
                loader.Progress = 0.8d;
            }

            Directory.Delete(cacheFolder, true);
            OpenContainingFolder(packPath);
        })
        {
            ProgressWeight = 6d
        });

        #endregion

        // 启动
        var mainLoader = new ModLoader.LoaderCombo<string>(loaderName, loaders)
            { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
        mainLoader.Start();
        ModLoader.LoaderTaskbarAdd(mainLoader);
        Navigator.Go(PageType.TaskManager);
    }

    private static void CopyIfExists(string source, string target)
    {
        if (!File.Exists(source))
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, true);
    }

    /// <summary>
    ///     在 UI 线程显示确认框，并阻塞后台导出线程直到用户作出选择。
    /// </summary>
    private static int AskOnUiThread(string message, string title) =>
        Dispatcher.UIThread.InvokeAsync(() => MsgBox.ShowAsync(message, title, Lang.Text("Common.Action.Continue"),
            Lang.Text("Common.Action.Cancel"))).GetAwaiter().GetResult();

    #endregion

    #region 文件对话框与打开位置

    private TopLevel? TopLevelOrNull => TopLevel.GetTopLevel(this);

    /// <summary>
    ///     显示保存文件对话框。<paramref name="filter"/> 为 "名称|*.扩展名|名称|*.扩展名" 形式。
    /// </summary>
    private async Task<string?> SelectSaveFileAsync(string title, string suggestedName, string filter)
    {
        var top = TopLevelOrNull;
        if (top is null)
            return null;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = ParseFileFilter(filter)
        });
        return file?.Path.LocalPath;
    }

    /// <summary>
    ///     显示打开文件对话框，只允许选择单个文件。
    /// </summary>
    private async Task<string?> SelectOpenFileAsync(string filter, string title)
    {
        var top = TopLevelOrNull;
        if (top is null)
            return null;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = ParseFileFilter(filter)
        });
        return files.FirstOrDefault()?.Path.LocalPath;
    }

    private static List<FilePickerFileType>? ParseFileFilter(string filter)
    {
        var parts = filter.Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;
        var result = new List<FilePickerFileType>();
        for (var i = 0; i + 1 < parts.Length; i += 2)
            result.Add(new FilePickerFileType(parts[i]) { Patterns = parts[i + 1].Split(';') });
        return result;
    }

    /// <summary>
    ///     在文件管理器中打开文件所在的文件夹。
    /// </summary>
    private void OpenContainingFolder(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(folder) || TopLevelOrNull is not { } top)
            return;
        Dispatcher.UIThread.Post(() => _ = top.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folder)));
    }

    #endregion
}
