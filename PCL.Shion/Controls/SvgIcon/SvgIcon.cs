using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public class SvgIcon : Control
{
    public static readonly StyledProperty<string> IconProperty =
        AvaloniaProperty.Register<SvgIcon, string>(nameof(Icon), string.Empty);

    public static readonly StyledProperty<string> DefaultPackProperty =
        AvaloniaProperty.Register<SvgIcon, string>(nameof(DefaultPack), SvgIconLoader.DefaultIconPack);

    public static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<SvgIcon, IBrush?>(nameof(IconBrush));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<SvgIcon, double>(
            nameof(StrokeThickness), 2D, validate: value => !double.IsNaN(value) && value >= 0D);

    public static readonly StyledProperty<bool> UseOriginalColorProperty =
        AvaloniaProperty.Register<SvgIcon, bool>(nameof(UseOriginalColor));

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<SvgIcon, Stretch>(nameof(Stretch), Stretch.Uniform);

    private SvgIconModel? _model;
    private bool _modelLoaded;

    static SvgIcon()
    {
        AffectsMeasure<SvgIcon>(IconProperty, DefaultPackProperty, StretchProperty);
        AffectsRender<SvgIcon>(
            IconProperty, DefaultPackProperty, IconBrushProperty,
            StrokeThicknessProperty, UseOriginalColorProperty, StretchProperty);
    }

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string DefaultPack
    {
        get => GetValue(DefaultPackProperty);
        set => SetValue(DefaultPackProperty, value);
    }

    public IBrush? IconBrush
    {
        get => GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool UseOriginalColor
    {
        get => GetValue(UseOriginalColorProperty);
        set => SetValue(UseOriginalColorProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public void AnimateIconBrushTo(
        MyColor color,
        TimeSpan? duration = null,
        ModAnimation.AniEase? easing = null,
        string? animationKey = null)
    {
        _EnsureAnimatableIconBrush();

        ModAnimation.AniStart(
            ModAnimation.AaColor(
                this,
                IconBrushProperty,
                color - IconBrush,
                (int)Math.Round((duration ?? TimeSpan.FromMilliseconds(120)).TotalMilliseconds),
                ease: easing ?? new ModAnimation.AniEaseOutFluent()),
            animationKey ?? $"SvgIconColor {RuntimeHelpers.GetHashCode(this)}");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty || change.Property == DefaultPackProperty)
        {
            _model = null;
            _modelLoaded = false;
        }
        else if (change.Property == TextElement.ForegroundProperty && IconBrush is null)
        {
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var model = _GetModel();
        var naturalSize = model is null
            ? new Size(24D, 24D)
            : new Size(model.Width, model.Height);

        if (double.IsInfinity(availableSize.Width) && double.IsInfinity(availableSize.Height))
            return naturalSize;

        if (double.IsInfinity(availableSize.Width))
            return new Size(naturalSize.Width * availableSize.Height / naturalSize.Height, availableSize.Height);

        if (double.IsInfinity(availableSize.Height))
            return new Size(availableSize.Width, naturalSize.Height * availableSize.Width / naturalSize.Width);

        return availableSize;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var model = _GetModel();
        var renderSize = Bounds.Size;
        if (model is null || model.Elements.Count == 0 || renderSize.Width <= 0D || renderSize.Height <= 0D)
            return;

        var target = _CalculateTargetRect(new Size(model.Width, model.Height), renderSize, Stretch);
        if (target.Width <= 0D || target.Height <= 0D)
            return;

        var scaleX = target.Width / model.Width;
        var scaleY = target.Height / model.Height;

        var transform = Matrix.CreateTranslation(-model.MinX, -model.MinY)
                        * Matrix.CreateScale(scaleX, scaleY)
                        * Matrix.CreateTranslation(target.X, target.Y);

        var iconBrush = IconBrush ?? GetValue(TextElement.ForegroundProperty) ?? Brushes.Black;
        var options = new SvgIconPaintOptions(iconBrush, StrokeThickness, UseOriginalColor);
        using (context.PushTransform(transform))
        {
            foreach (var element in model.Elements)
                element.Draw(context, options);
        }
    }

    private SvgIconModel? _GetModel()
    {
        if (_modelLoaded)
            return _model;

        _model = SvgIconLoader.Load(Icon, DefaultPack);
        _modelLoaded = true;
        return _model;
    }

    // 与 WPF 的 IconBrush = ... 相同：替换属性上原有的资源引用，并保证动画起点是一个可修改的纯色画刷
    private void _EnsureAnimatableIconBrush()
    {
        if (IconBrush is SolidColorBrush)
            return;

        ResourceReferenceExtensions.SetValueReplacingBinding(this, IconBrushProperty, IconBrush switch
        {
            ISolidColorBrush solidColorBrush => new SolidColorBrush(solidColorBrush.Color),
            null => new SolidColorBrush(
                (GetValue(TextElement.ForegroundProperty) as ISolidColorBrush)?.Color ?? Colors.Black),
            _ => new SolidColorBrush(Colors.Black)
        });
    }

    private static Rect _CalculateTargetRect(Size sourceSize, Size renderSize, Stretch stretch)
    {
        if (sourceSize.Width <= 0D || sourceSize.Height <= 0D)
            sourceSize = new Size(24D, 24D);

        if (stretch == Stretch.None)
        {
            var x = (renderSize.Width - sourceSize.Width) / 2D;
            var y = (renderSize.Height - sourceSize.Height) / 2D;
            return new Rect(x, y, sourceSize.Width, sourceSize.Height);
        }

        var scaleX = renderSize.Width / sourceSize.Width;
        var scaleY = renderSize.Height / sourceSize.Height;

        var scale = stretch switch
        {
            Stretch.Fill => double.NaN,
            Stretch.UniformToFill => Math.Max(scaleX, scaleY),
            _ => Math.Min(scaleX, scaleY)
        };

        var width = stretch == Stretch.Fill ? renderSize.Width : sourceSize.Width * scale;
        var height = stretch == Stretch.Fill ? renderSize.Height : sourceSize.Height * scale;
        var left = (renderSize.Width - width) / 2D;
        var top = (renderSize.Height - height) / 2D;

        return new Rect(left, top, width, height);
    }
}
