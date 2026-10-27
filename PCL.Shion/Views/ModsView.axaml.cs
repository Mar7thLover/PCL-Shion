using Avalonia.Controls;
using PCL.Shion.ViewModels;

namespace PCL.Shion.Views;

public partial class ModsView : UserControl
{
    public ModsView()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as ModsViewModel)?.CancelScan();
    }
}
