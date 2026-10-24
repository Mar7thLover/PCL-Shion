using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public class AnimatedBackgroundGrid : Grid
{
    public static readonly StyledProperty<SolidColorBrush> BackgroundBrushProperty =
        AvaloniaProperty.Register<AnimatedBackgroundGrid, SolidColorBrush>(nameof(BackgroundBrush),
            new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)));

    private readonly AvaloniaProperty _animatableBrushProperty;

    public readonly int uuid = GetUuid();

    static AnimatedBackgroundGrid()
    {
        BackgroundBrushProperty.Changed.AddClassHandler<AnimatedBackgroundGrid>((grid, e) =>
            _BackgroundBrushChanged(grid, e));
    }

    public AnimatedBackgroundGrid(AvaloniaProperty brushDp)
    {
        _animatableBrushProperty = brushDp;
        Loaded += (_, _) => Init();
    }

    public AnimatedBackgroundGrid() : this(Panel.BackgroundProperty)
    {
    }

    protected virtual Control AnimatableElement => this;

    protected virtual SolidColorBrush AnimatableBrush
    {
        get => (SolidColorBrush)Background!;
        set => Background = value;
    }

    protected bool IsAnimating
    {
        get => field;
        private set => field = value;
    }

    public SolidColorBrush BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    private static void _BackgroundBrushChanged(AnimatedBackgroundGrid grid, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not SolidColorBrush brush)
            return;
        if (!(grid.IsLoaded && grid.IsVisible))
        {
            grid.AnimatableBrush = brush;
            return;
        }

        Dispatcher.UIThread.Post(async () =>
        {
            grid.IsAnimating = true;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(grid.AnimatableElement, grid._animatableBrushProperty,
                        new MyColor(brush) - new MyColor(grid.AnimatableBrush), 300)
                }, "MyCard Theme " + grid.uuid);
            await Task.Delay(300);
            grid.AnimatableBrush = brush;
            grid.IsAnimating = false;
        });
    }

    private void Init()
    {
        AnimatableBrush = BackgroundBrush;
    }

    private static int uuidSeed;

    private static int GetUuid() => Interlocked.Increment(ref uuidSeed);
}
