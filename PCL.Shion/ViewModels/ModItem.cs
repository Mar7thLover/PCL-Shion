using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Shion.Core.Mods;

namespace PCL.Shion.ViewModels;

/// <summary>
/// Mod 列表中的一项。用户拨动启用开关时，由所属的 <see cref="ModsViewModel"/> 去改文件名；
/// 删除分两步，先在这一行上确认，再真正移到回收站。
/// </summary>
public sealed partial class ModItem(LocalMod mod, ModsViewModel owner) : ObservableObject
{
    private bool _enabled = mod.Enabled;

    public LocalMod Mod { get; private set; } = mod;

    /// <summary>优先显示 Mod 自己声明的名称，其次是 ID，都没有时用文件名。</summary>
    public string DisplayName => Mod.Name ?? Mod.Id ?? FileStem(Mod.FileName);

    /// <summary>列表第二行：版本、加载器与文件名。</summary>
    public string Description => string.Join(" · ",
        new[] { Mod.Version, LoadersText, Mod.FileName, Mod.ReadWarning is null ? null : ModsStrings.PartialMetadata }
            .Where(part => !string.IsNullOrEmpty(part)));

    /// <summary>启用开关的状态；由界面改动时会触发改名，改名失败后用 <see cref="RestoreEnabled"/> 还原。</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (SetProperty(ref _enabled, value))
                _ = owner.ApplyEnabledAsync(this);
        }
    }

    /// <summary>正在改名或删除，此时这一行不能再操作。</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsDeletePending { get; set; }

    public bool Matches(string keyword) =>
        Mod.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true
        || Mod.Id?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true
        || Mod.FileName.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    /// <summary>文件改名后换上新的 Mod 信息。</summary>
    public void Replace(LocalMod updated)
    {
        Mod = updated;
        OnPropertyChanged(nameof(Description));
    }

    /// <summary>不触发改名，把开关还原成文件的实际状态。</summary>
    public void RestoreEnabled() => SetProperty(ref _enabled, Mod.Enabled, nameof(Enabled));

    [RelayCommand]
    private void RequestDelete() => owner.RequestDelete(this);

    [RelayCommand]
    private Task ConfirmDeleteAsync() => owner.DeleteAsync(this);

    [RelayCommand]
    private void CancelDelete() => IsDeletePending = false;

    private string LoadersText => string.Join(" / ", Mod.Loaders.Select(FormatLoader));

    private static string FormatLoader(string loader) => loader.ToLowerInvariant() switch
    {
        "forge" => "Forge",
        "neoforge" => "NeoForge",
        "fabric" => "Fabric",
        "quilt" => "Quilt",
        _ => loader,
    };

    private static string FileStem(string fileName)
    {
        var name = ModScanner.GetEnabledFileName(fileName);
        return Path.GetFileNameWithoutExtension(name);
    }
}
