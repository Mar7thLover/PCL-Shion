using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Humanizer;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Core.Saves;
using PCL.Shion.Localization;
using PCL.Shion.UI;

namespace PCL.Shion.Pages.Instance.Saves;

public partial class PageInstanceSavesInfo : MyPageRight, IRefreshable
{
    /// <summary>无状态服务，线程安全，所有实例可共享。</summary>
    private static readonly SaveManager SaveManager = new();

    /// <summary>防并发冲突</summary>
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private CancellationTokenSource? _cts;

    public PageInstanceSavesInfo()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Loaded += async (_, _) =>
        {
            PanBack.ScrollToHome();
            await RefreshInfoAsync();
        };
    }

    void IRefreshable.Refresh() => Refresh();

    public void Refresh() => _ = RefreshInfoAsync();

    private async Task RefreshInfoAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            ClearInfoTable();
            PanSettingsList.Children.Clear();
            PanSettingsList.RowDefinitions.Clear();
            Hintversion1_9.IsVisible = false;
            Hintversion1_8.IsVisible = false;
            Hintversion1_3.IsVisible = false;
            PanSettings.IsVisible = false;

            var save = await SaveManager.LoadSaveAsync(PageInstanceSavesLeft.currentSave, ct);

            if (PageInstanceSavesLeft.frmInstanceSavesLeft is { } left)
                left.ItemDatapack.IsVisible = !(save.VersionId is null or < DataVersionBoundaries._17w47a);

            if (save.VersionName is null)
            {
                if (save.Difficulty.HasValue)
                    ShowHint(Hintversion1_9, "Instance.Saves.Info.VersionHint.1_9");
                else if (save.AllowCommands)
                    ShowHint(Hintversion1_8, "Instance.Saves.Info.VersionHint.1_8");
                else
                    ShowHint(Hintversion1_3, "Instance.Saves.Info.VersionHint.1_3");
            }
            else
                AddInfoRow(Lang.Text("Instance.Saves.Info.Version"), $"{save.VersionName} ({save.VersionId})");

            AddInfoRow(Lang.Text("Instance.Saves.Info.LevelName"), save.LevelName);
            AddInfoRow(Lang.Text("Instance.Saves.Info.Seed"),
                save.Seed?.ToString() ?? Lang.Text("Instance.Saves.Info.GetFailed"),
                isSeed: true, versionName: save.VersionName);
            AddInfoRow(Lang.Text("Instance.Saves.Info.LastPlayed"),
                Lang.Date(save.LastPlayedUtc.ToLocalTime(), "g"));

            if (save.Spawn.HasValue)
            {
                var s = save.Spawn.Value;
                AddInfoRow(Lang.Text("Instance.Saves.Info.SpawnPoint"), $"{s.X:F0} / {s.Y:F0} / {s.Z:F0}");
            }

            AddInfoRow(Lang.Text("Instance.Saves.Info.GameMode"), GameModeName(save.GameMode));

            AddInfoRow(Lang.Text("Instance.Saves.Info.PlayTime"),
                Lang.TimeSpan(save.PlayTime, 3, false, TimeUnit.Day, TimeUnit.Second));

            if (save.VersionName is not null || save.Difficulty.HasValue)
                BuildAllowCommandsSetting(save.AllowCommands);

            if (save.Difficulty.HasValue)
                BuildDifficultySetting(save.IsHardcore, save.IsDifficultyLocked, (int)save.Difficulty.Value);

            PanContent.IsVisible = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            var message = Lang.Text("Instance.Saves.Info.Error.LoadFailed");
            SavesSupport.LogFailure(ex, message);
            _ = MsgBox.ShowAsync(message, isWarn: true);
            PanContent.IsVisible = false;
            PanSettings.IsVisible = false;
            PanSettingsList.Children.Clear();
            PanSettingsList.RowDefinitions.Clear();
            Hintversion1_9.IsVisible = false;
            Hintversion1_8.IsVisible = false;
            Hintversion1_3.IsVisible = false;
        }
    }

    private void BuildAllowCommandsSetting(bool allowCommands)
    {
        PanSettings.IsVisible = true;
        var folder = PageInstanceSavesLeft.currentSave;

        var combo = new MyComboBox
        {
            Width = 100d, HorizontalAlignment = HorizontalAlignment.Left,
        };
        ToolTip.SetTip(combo, Lang.Text("Instance.Saves.Info.Modify.BeforeSave"));
        combo.Items.Add(new MyComboBoxItem { Content = Lang.Text("Instance.Saves.Info.AllowCommands.NotAllowed"), Tag = 0 });
        combo.Items.Add(new MyComboBoxItem { Content = Lang.Text("Instance.Saves.Info.AllowCommands.Allowed"), Tag = 1 });
        combo.SelectedIndex = allowCommands ? 1 : 0;

        combo.SelectionChanged += async (_, _) =>
        {
            try
            {
                if (GetSelectedTag(combo) is not { } selected) return;
                await WriteLock.WaitAsync();
                try
                {
                    await SaveManager.ApplyChangesAsync(folder, new SaveChanges
                    {
                        AllowCommands = new Editable<bool>(selected == 1),
                    });
                }
                finally
                {
                    WriteLock.Release();
                }

                HintService.Hint(Lang.Text("Instance.Saves.Info.Modify.CheatSuccess"), HintType.Success);
            }
            catch (Exception ex)
            {
                var message = Lang.Text("Instance.Saves.Info.Modify.CheatFailed");
                SavesSupport.LogFailure(ex, message, message);
            }
        };

        AddSettingRow(Lang.Text("Instance.Saves.Info.AllowCommands"), combo);
    }

    private void BuildDifficultySetting(bool isHardcore, bool isLocked, int difficultyValue)
    {
        PanSettings.IsVisible = true;
        var folder = PageInstanceSavesLeft.currentSave;

        var combo = new MyComboBox
        {
            Width = 100d, HorizontalAlignment = HorizontalAlignment.Left,
        };
        ToolTip.SetTip(combo, Lang.Text("Instance.Saves.Info.Modify.BeforeSave"));
        combo.Items.Add(new MyComboBoxItem { Content = Lang.Text("Instance.Saves.Info.Difficulty.Peaceful"), Tag = 0 });
        combo.Items.Add(new MyComboBoxItem { Content = Lang.Text("Instance.Saves.Info.Difficulty.Easy"), Tag = 1 });
        combo.Items.Add(new MyComboBoxItem { Content = Lang.Text("Instance.Saves.Info.Difficulty.Normal"), Tag = 2 });
        combo.Items.Add(new MyComboBoxItem { Content = Lang.Text("Instance.Saves.Info.Difficulty.Hard"), Tag = 3 });
        combo.SelectedIndex = difficultyValue;

        var lockCheckBox = new MyCheckBox
        {
            Text = Lang.Text("Instance.Saves.Info.LockDifficulty"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10d, 0d, 0d, 0d),
            Checked = isLocked,
            IsVisible = !isHardcore,
        };
        ToolTip.SetTip(lockCheckBox, Lang.Text("Instance.Saves.Info.LockDifficulty.ToolTip"));

        var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(combo);
        panel.Children.Add(lockCheckBox);

        async Task ApplyAsync()
        {
            try
            {
                if (GetSelectedTag(combo) is not { } selected) return;
                await WriteLock.WaitAsync();
                try
                {
                    await SaveManager.ApplyChangesAsync(folder, new SaveChanges
                    {
                        Difficulty = new Editable<Difficulty>((Difficulty)selected),
                        LockDifficulty = new Editable<bool>(!isHardcore && lockCheckBox.Checked == true),
                    });
                }
                finally
                {
                    WriteLock.Release();
                }

                HintService.Hint(Lang.Text("Instance.Saves.Info.Modify.DifficultySuccess"), HintType.Success);
            }
            catch (Exception ex)
            {
                var message = Lang.Text("Instance.Saves.Info.Modify.DifficultyFailed");
                SavesSupport.LogFailure(ex, message, message);
            }
        }

        combo.SelectionChanged += async (_, _) => await ApplyAsync();
        lockCheckBox.Change += async (_, _) => await ApplyAsync();

        AddSettingRow(Lang.Text("Instance.Saves.Info.GameDifficultyLabel"), panel);
    }

    /// <summary>
    ///     取得下拉框当前选中项的 Tag（数值选项的值），没有选中或类型不符时返回 null。
    /// </summary>
    private static int? GetSelectedTag(MyComboBox combo) =>
        combo.SelectedItem is MyComboBoxItem { Tag: int value } ? value : null;

    private static string GameModeName(GameMode mode) => mode switch
    {
        GameMode.Hardcore => Lang.Text("Instance.Saves.Info.GameMode.Hardcore"),
        GameMode.Creative => Lang.Text("Instance.Saves.Info.GameMode.Creative"),
        GameMode.Adventure => Lang.Text("Instance.Saves.Info.GameMode.Adventure"),
        GameMode.Spectator => Lang.Text("Instance.Saves.Info.GameMode.Spectator"),
        _ => Lang.Text("Instance.Saves.Info.GameMode.Survival"),
    };

    private static void ShowHint(MyHint hint, string langKey)
    {
        hint.Text = Lang.Text(langKey);
        hint.IsVisible = true;
    }

    private void ClearInfoTable()
    {
        PanList.Children.Clear();
        PanList.RowDefinitions.Clear();
    }

    private void AddInfoRow(string head, string content, bool isSeed = false, string? versionName = null)
    {
        var headBlock = new TextBlock { Text = head, Margin = new Thickness(0d, 3d, 0d, 3d) };
        var contentStack = new StackPanel { Orientation = Orientation.Horizontal };

        if (isSeed && content != Lang.Text("Instance.Saves.Info.GetFailed"))
        {
            var seedBtn = new MyTextButton { Text = content, Margin = new Thickness(0d, 3d, 0d, 3d) };
            seedBtn.Click += (_, _) => _ = CopyTextAsync(content);
            contentStack.Children.Add(seedBtn);

            var chunkbaseBtn = new MyIconButton
            {
                SvgIcon = "lucide/external-link",
                Width = 22d,
                Height = 22d,
            };
            ToolTip.SetTip(chunkbaseBtn, Lang.Text("Instance.Saves.Info.Chunkbase.ToolTip"));
            chunkbaseBtn.Click += (_, _) => OpenChunkbase(content, versionName);
            contentStack.Children.Add(chunkbaseBtn);
        }
        else
        {
            contentStack.Children.Add(new TextBlock { Text = content, Margin = new Thickness(0d, 3d, 0d, 3d) });
        }

        PanList.Children.Add(headBlock);
        PanList.Children.Add(contentStack);
        var rowDef = new RowDefinition();
        PanList.RowDefinitions.Add(rowDef);
        var rowIndex = PanList.RowDefinitions.IndexOf(rowDef);
        Grid.SetRow(headBlock, rowIndex);
        Grid.SetColumn(headBlock, 0);
        Grid.SetRow(contentStack, rowIndex);
        Grid.SetColumn(contentStack, 2);
    }

    private void AddSettingRow(string head, Control control)
    {
        var rowIndex = PanSettingsList.RowDefinitions.Count;
        PanSettingsList.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Auto) });

        var headBlock = new TextBlock { Text = head, Margin = new Thickness(0d, 3d, 0d, 3d) };
        Grid.SetRow(headBlock, rowIndex);
        Grid.SetColumn(headBlock, 0);
        Grid.SetRow(control, rowIndex);
        Grid.SetColumn(control, 2);
        PanSettingsList.Children.Add(headBlock);
        PanSettingsList.Children.Add(control);
        PanSettingsList.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8d, GridUnitType.Pixel) });
    }

    /// <summary>
    ///     把文本写入剪贴板。
    /// </summary>
    private async Task CopyTextAsync(string text)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard
                            ?? throw new InvalidOperationException("Clipboard is unavailable.");
            await clipboard.SetTextAsync(text);
        }
        catch (Exception ex)
        {
            var message = Lang.Text("Instance.Saves.Info.Error.ClipboardFailed");
            SavesSupport.LogFailure(ex, message, message);
        }
    }

    private static void OpenChunkbase(string seed, string? versionName)
    {
        try
        {
            if (versionName is null)
            {
                HintService.Hint(Lang.Text("Instance.Saves.Info.Chunkbase.UnknownVersion"));
                return;
            }

            if (versionName.Any(char.IsLetter))
            {
                HintService.Hint(Lang.Text("Instance.Saves.Info.Chunkbase.PreviewVersion", versionName));
                return;
            }

            var usedVersion = versionName.StartsWith("1.21")
                ? versionName.Replace(".", "_")
                : versionName.Contains('.')
                    ? string.Join("_", versionName.Split('.').Take(2))
                    : versionName.Replace(".", "_");
            SystemShell.OpenUrl(
                $"https://www.chunkbase.com/apps/seed-map#seed={seed}&platform=java_{usedVersion}&dimension=overworld");
        }
        catch (Exception ex)
        {
            var message = Lang.Text("Instance.Saves.Info.Error.ChunkbaseFailed");
            SavesSupport.LogFailure(ex, message, message);
        }
    }
}
