using System.Threading.Tasks;
using PCL.Shion.Services;

namespace PCL.Shion.ViewModels;

public sealed class MainWindowViewModel(AppSettings settings)
{
    public HomeViewModel Home { get; } = new(settings);

    public Task InitializeAsync() => Home.InitializeAsync();
}
