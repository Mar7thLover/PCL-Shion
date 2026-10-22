using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

public sealed partial class MainWindowViewModel(GameServices services) : ObservableObject
{
    public HomeViewModel Home { get; } = new(services);

    public DownloadViewModel Download { get; } = new(services);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLaunchPage))]
    public partial bool IsDownloadPage { get; set; }

    public bool IsLaunchPage => !IsDownloadPage;

    public Task InitializeAsync() => Home.InitializeAsync();

    [RelayCommand]
    private void ShowLaunch() => IsDownloadPage = false;

    [RelayCommand]
    private void ShowDownload() => IsDownloadPage = true;
}
