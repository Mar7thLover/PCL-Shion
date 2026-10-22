using System;
using Avalonia.Controls;
using PCL.Shion.ViewModels;

namespace PCL.Shion;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await ((MainWindowViewModel)DataContext!).InitializeAsync();
    }
}
