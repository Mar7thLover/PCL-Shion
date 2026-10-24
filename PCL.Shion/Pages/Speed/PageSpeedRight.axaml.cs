using Avalonia.Controls;

namespace PCL.Shion.Pages.Speed;

public partial class PageSpeedRight : MyPageRight
{
    public PageSpeedRight()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Loaded += (_, _) => Init();
    }

    /// <summary>任务卡片所在的面板，对应 WPF 的 PanMain。</summary>
    internal StackPanel CardPanel => PanMain;

    private void Init()
    {
        PanBack.ScrollToVerticalOffset(0d);
    }
}
