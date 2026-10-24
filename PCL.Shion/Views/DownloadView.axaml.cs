using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PCL.Shion.ViewModels;

namespace PCL.Shion.Views;

public partial class DownloadView : UserControl
{
    public DownloadView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        LoadVersions();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty)
            LoadVersions();
    }

    /// <summary>页面第一次真正显示时才去获取版本清单。</summary>
    private void LoadVersions()
    {
        if (IsLoaded && IsVisible && DataContext is DownloadViewModel viewModel)
            _ = viewModel.EnsureLoadedAsync();
    }
}
