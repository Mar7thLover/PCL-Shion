using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PCL.Shion.ViewModels;

namespace PCL.Shion.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void OnAddFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = SettingsStrings.PickFolderTitle });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            ((SettingsViewModel)DataContext!).AddFolder(path);
    }
}
