using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Shion.Core.Instances;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.ViewModels;

// 重命名、删除与打开目录
public sealed partial class InstanceViewModel
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    public partial string NewName { get; set; } = "";

    /// <summary>新名称不可用的原因；可用时为空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRenameError))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    public partial string? RenameError { get; set; }

    /// <summary>重命名、删除或打开目录的结果提示；下一次操作开始时清除。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasManageMessage))]
    public partial string? ManageMessage { get; set; }

    [ObservableProperty]
    public partial bool ManageMessageIsError { get; set; }

    /// <summary>点了“删除实例”、正在等待二次确认时为 true。</summary>
    [ObservableProperty]
    public partial bool IsConfirmingDelete { get; set; }

    [ObservableProperty]
    public partial string DeleteConfirmText { get; set; } = "";

    public bool HasRenameError => RenameError is not null;

    public bool HasManageMessage => ManageMessage is not null;

    partial void OnNewNameChanged(string value) => RenameError = ValidateNewName();

    /// <summary>检查新名称，没有问题时返回 null。</summary>
    private string? ValidateNewName()
    {
        var name = NewName.Trim();
        if (name.Length == 0)
            return InstanceStrings.NameEmpty;
        if (!InstanceManager.IsValidName(name))
            return InstanceStrings.NameInvalid;
        return name != Name && Directory.Exists(Path.Combine(MinecraftRoot, "versions", name)) ? InstanceStrings.NameExists : null;
    }

    private bool CanRename() => IsLoaded && !IsBusy && RenameError is null && NewName.Trim() != Name;

    [RelayCommand(CanExecute = nameof(CanRename))]
    private async Task RenameAsync()
    {
        ManageMessage = null;
        if (RejectWhileRunning())
            return;

        var root = MinecraftRoot;
        var oldName = Name;
        var newName = NewName.Trim();
        IsConfirmingDelete = false;
        IsBusy = true;
        try
        {
            await Task.Run(() => InstanceManager.Rename(root, oldName, newName));
            Name = newName;
            NewName = newName;

            // 主页靠名称记住选中的实例，改名后要跟着换，否则选中会丢
            if (_services.Settings.SelectedInstance == oldName)
            {
                _services.Settings.SelectedInstance = newName;
                _services.Settings.Save();
            }

            _services.NotifyInstancesChanged();
            await LoadAsync();
            ShowManageMessage(InstanceStrings.Renamed(newName), isError: false);
        }
        catch (Exception e)
        {
            ShowManageMessage(InstanceStrings.RenameFailed(e.Message), isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanManage() => IsLoaded && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanManage))]
    private void RequestDelete()
    {
        ManageMessage = null;
        if (RejectWhileRunning())
            return;

        DeleteConfirmText = InstanceStrings.DeleteConfirm(Name, _instance!.Directory);
        IsConfirmingDelete = true;
    }

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    /// <summary>把实例目录移到回收站，成功后通知外壳离开本页。</summary>
    [RelayCommand(CanExecute = nameof(CanManage))]
    private async Task ConfirmDeleteAsync()
    {
        if (RejectWhileRunning())
        {
            IsConfirmingDelete = false;
            return;
        }

        var root = MinecraftRoot;
        var name = Name;
        IsBusy = true;
        try
        {
            await Task.Run(() => InstanceManager.Delete(root, name));
        }
        catch (Exception e)
        {
            ShowManageMessage(InstanceStrings.DeleteFailed(e.Message), isError: true);
            return;
        }
        finally
        {
            IsConfirmingDelete = false;
            IsBusy = false;
        }

        _services.NotifyInstancesChanged();
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenInstanceFolder() => OpenFolder(_instance!.Directory);

    [RelayCommand]
    private void OpenGameFolder() => OpenFolder(_gameDirectory);

    private void OpenFolder(string path)
    {
        ManageMessage = null;
        try
        {
            SystemShell.OpenFolder(path);
        }
        catch (Win32Exception e)
        {
            ShowManageMessage(InstanceStrings.OpenFailed(e.Message), isError: true);
        }
    }

    /// <summary>游戏正在运行时实例目录被占用，重命名和删除会失败，所以先拦下并给出明确的提示。</summary>
    private bool RejectWhileRunning()
    {
        if (!_services.Runner.Running.Any(game => game.InstanceName == Name))
            return false;

        ShowManageMessage(InstanceStrings.RunningBlocked, isError: true);
        return true;
    }

    private void ShowManageMessage(string text, bool isError)
    {
        ManageMessageIsError = isError;
        ManageMessage = text;
    }
}
