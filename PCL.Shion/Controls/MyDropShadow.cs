using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Shion.Controls;

public class MyDropShadow : Decorator
{
    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<MyDropShadow, Color>(nameof(Color), Color.FromArgb(0x71, 0x0, 0x0, 0x0));

    public static readonly StyledProperty<double> ShadowRadiusProperty =
        AvaloniaProperty.Register<MyDropShadow, double>(nameof(ShadowRadius), 5d);

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MyDropShadow, CornerRadius>(nameof(CornerRadius), new CornerRadius(),
            validate: IsCornerRadiusValid);

    private static IBrush[]? _commonBrushes;
    private static CornerRadius _commonCornerRadius;
    private static readonly object _resourceAccess = new();
    private IBrush[]? _brushes;

    static MyDropShadow()
    {
        AffectsRender<MyDropShadow>(ColorProperty, ShadowRadiusProperty, CornerRadiusProperty);
        ColorProperty.Changed.AddClassHandler<MyDropShadow>((s, _) => s._brushes = null);
        ShadowRadiusProperty.Changed.AddClassHandler<MyDropShadow>((s, _) => s._brushes = null);
        CornerRadiusProperty.Changed.AddClassHandler<MyDropShadow>((s, _) => s._brushes = null);
    }

    /// <summary>
    ///     阴影颜色。
    /// </summary>
    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>
    ///     阴影模糊半径。
    /// </summary>
    public double ShadowRadius
    {
        get => GetValue(ShadowRadiusProperty);
        set => SetValue(ShadowRadiusProperty, value);
    }

    /// <summary>
    ///     圆角大小。
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private static bool IsCornerRadiusValid(CornerRadius cr)
    {
        return !(cr.TopLeft < 0.0d || cr.TopRight < 0.0d || cr.BottomLeft < 0.0d || cr.BottomRight < 0.0d ||
                 double.IsNaN(cr.TopLeft) || double.IsNaN(cr.TopRight) || double.IsNaN(cr.BottomLeft) ||
                 double.IsNaN(cr.BottomRight) || double.IsInfinity(cr.TopLeft) || double.IsInfinity(cr.TopRight) ||
                 double.IsInfinity(cr.BottomLeft) || double.IsInfinity(cr.BottomRight));
    }


    // =======================================
    // 渲染
    // =======================================


    public override void Render(DrawingContext context)
    {
        var cornerRadius = CornerRadius;
        var shadowBounds = new Rect(0d, 0d, Bounds.Width, Bounds.Height);
        var color = Color;

        if (shadowBounds.Width > 0d && shadowBounds.Height > 0d && color.A > 0)
        {
            var centerWidth = shadowBounds.Right - shadowBounds.Left - 2d * ShadowRadius;
            var centerHeight = shadowBounds.Bottom - shadowBounds.Top - 2d * ShadowRadius;
            var maxRadius = Math.Min(centerWidth * 0.5d, centerHeight * 0.5d);
            cornerRadius = new CornerRadius(
                Math.Min(cornerRadius.TopLeft, maxRadius),
                Math.Min(cornerRadius.TopRight, maxRadius),
                Math.Min(cornerRadius.BottomRight, maxRadius),
                Math.Min(cornerRadius.BottomLeft, maxRadius));
            var brushes = GetBrushes(color, cornerRadius);
            var centerTop = shadowBounds.Top + ShadowRadius;
            var centerLeft = shadowBounds.Left + ShadowRadius;
            var centerRight = shadowBounds.Right - ShadowRadius;
            var centerBottom = shadowBounds.Bottom - ShadowRadius;
            var guidelineSetX = new[]
            {
                centerLeft, centerLeft + cornerRadius.TopLeft, centerRight - cornerRadius.TopRight,
                centerLeft + cornerRadius.BottomLeft, centerRight - cornerRadius.BottomRight, centerRight
            };
            var guidelineSetY = new[]
            {
                centerTop, centerTop + cornerRadius.TopLeft, centerTop + cornerRadius.TopRight,
                centerBottom - cornerRadius.BottomLeft, centerBottom - cornerRadius.BottomRight, centerBottom
            };
            cornerRadius = new CornerRadius(
                cornerRadius.TopLeft + ShadowRadius,
                cornerRadius.TopRight + ShadowRadius,
                cornerRadius.BottomRight + ShadowRadius,
                cornerRadius.BottomLeft + ShadowRadius);
            var topLeft = new Rect(shadowBounds.Left, shadowBounds.Top, cornerRadius.TopLeft, cornerRadius.TopLeft);
            context.DrawRectangle(brushes[(int)Placement.TopLeft], null, topLeft);
            var topWidth = guidelineSetX[2] - guidelineSetX[1];

            if (topWidth > 0d)
            {
                var top = new Rect(guidelineSetX[1], shadowBounds.Top, topWidth, ShadowRadius);
                context.DrawRectangle(brushes[(int)Placement.Top], null, top);
            }

            var topRight = new Rect(guidelineSetX[2], shadowBounds.Top, cornerRadius.TopRight, cornerRadius.TopRight);
            context.DrawRectangle(brushes[(int)Placement.TopRight], null, topRight);
            var leftHeight = guidelineSetY[3] - guidelineSetY[1];

            if (leftHeight > 0d)
            {
                var left = new Rect(shadowBounds.Left, guidelineSetY[1], ShadowRadius, leftHeight);
                context.DrawRectangle(brushes[(int)Placement.Left], null, left);
            }

            var rightHeight = guidelineSetY[4] - guidelineSetY[2];

            if (rightHeight > 0d)
            {
                var right = new Rect(guidelineSetX[5], guidelineSetY[2], ShadowRadius, rightHeight);
                context.DrawRectangle(brushes[(int)Placement.Right], null, right);
            }

            var bottomLeft = new Rect(shadowBounds.Left, guidelineSetY[3], cornerRadius.BottomLeft,
                cornerRadius.BottomLeft);
            context.DrawRectangle(brushes[(int)Placement.BottomLeft], null, bottomLeft);
            var bottomWidth = guidelineSetX[4] - guidelineSetX[3];

            if (bottomWidth > 0d)
            {
                var bottom = new Rect(guidelineSetX[3], guidelineSetY[5], bottomWidth, ShadowRadius);
                context.DrawRectangle(brushes[(int)Placement.Bottom], null, bottom);
            }

            var bottomRight = new Rect(guidelineSetX[4], guidelineSetY[4], cornerRadius.BottomRight,
                cornerRadius.BottomRight);
            context.DrawRectangle(brushes[(int)Placement.BottomRight], null, bottomRight);

            if (cornerRadius.TopLeft == ShadowRadius && cornerRadius.TopLeft == cornerRadius.TopRight &&
                cornerRadius.TopLeft == cornerRadius.BottomLeft && cornerRadius.TopLeft == cornerRadius.BottomRight)
            {
                var center = new Rect(guidelineSetX[0], guidelineSetY[0], centerWidth, centerHeight);
                context.DrawRectangle(brushes[(int)Placement.Center], null, center);
            }
            else
            {
                var figure = new PathFigure();

                if (cornerRadius.TopLeft > ShadowRadius)
                {
                    figure.StartPoint = new Point(guidelineSetX[1], guidelineSetY[0]);
                    AddLine(figure, guidelineSetX[1], guidelineSetY[1]);
                    AddLine(figure, guidelineSetX[0], guidelineSetY[1]);
                }
                else
                {
                    figure.StartPoint = new Point(guidelineSetX[0], guidelineSetY[0]);
                }

                if (cornerRadius.BottomLeft > ShadowRadius)
                {
                    AddLine(figure, guidelineSetX[0], guidelineSetY[3]);
                    AddLine(figure, guidelineSetX[3], guidelineSetY[3]);
                    AddLine(figure, guidelineSetX[3], guidelineSetY[5]);
                }
                else
                {
                    AddLine(figure, guidelineSetX[0], guidelineSetY[5]);
                }

                if (cornerRadius.BottomRight > ShadowRadius)
                {
                    AddLine(figure, guidelineSetX[4], guidelineSetY[5]);
                    AddLine(figure, guidelineSetX[4], guidelineSetY[4]);
                    AddLine(figure, guidelineSetX[5], guidelineSetY[4]);
                }
                else
                {
                    AddLine(figure, guidelineSetX[5], guidelineSetY[5]);
                }

                if (cornerRadius.TopRight > ShadowRadius)
                {
                    AddLine(figure, guidelineSetX[5], guidelineSetY[2]);
                    AddLine(figure, guidelineSetX[2], guidelineSetY[2]);
                    AddLine(figure, guidelineSetX[2], guidelineSetY[0]);
                }
                else
                {
                    AddLine(figure, guidelineSetX[5], guidelineSetY[0]);
                }

                figure.IsClosed = true;
                var geometry = new PathGeometry();
                geometry.Figures!.Add(figure);
                context.DrawGeometry(brushes[(int)Placement.Center], null, geometry);
            }
        }
    }

    private static void AddLine(PathFigure figure, double x, double y)
    {
        figure.Segments!.Add(new LineSegment { Point = new Point(x, y), IsStroked = true });
    }

    private GradientStop[] CreateStops(Color c, double cornerRadius)
    {
        var gradientScale = 1d / (ShadowRadius + cornerRadius);
        var stopColor = c;
        var gsc = new List<GradientStop>
        {
            new GradientStop(stopColor, (ShadowRadius * 0.1d + cornerRadius) * gradientScale)
        };
        stopColor = WithAlpha(stopColor, (byte)Math.Round(0.74336d * c.A));
        gsc.Add(new GradientStop(stopColor, (ShadowRadius * 0.3d + cornerRadius) * gradientScale));
        stopColor = WithAlpha(stopColor, (byte)Math.Round(0.38053d * c.A));
        gsc.Add(new GradientStop(stopColor, (ShadowRadius * 0.5d + cornerRadius) * gradientScale));
        stopColor = WithAlpha(stopColor, (byte)Math.Round(0.12389d * c.A));
        gsc.Add(new GradientStop(stopColor, (ShadowRadius * 0.7d + cornerRadius) * gradientScale));
        stopColor = WithAlpha(stopColor, (byte)Math.Round(0.02654d * c.A));
        gsc.Add(new GradientStop(stopColor, (ShadowRadius * 0.9d + cornerRadius) * gradientScale));
        stopColor = WithAlpha(stopColor, 0);
        gsc.Add(new GradientStop(stopColor, (ShadowRadius + cornerRadius) * gradientScale));
        return gsc.ToArray();
    }

    /// <summary>
    ///     返回仅修改透明度后的颜色（Avalonia 的 Color 没有 WithAlpha 方法）。
    /// </summary>
    private static Color WithAlpha(Color c, byte alpha)
    {
        return Color.FromArgb(alpha, c.R, c.G, c.B);
    }

    private IBrush[] CreateBrushes(Color c, CornerRadius cornerRadius)
    {
        var brushes = new IBrush[9];
        brushes[(int)Placement.Center] = new SolidColorBrush(c);
        var sideStops = CreateStops(c, 0d);
        brushes[(int)Placement.Top] = CreateLinearBrush(sideStops,
            new RelativePoint(0d, 1d, RelativeUnit.Relative), new RelativePoint(0d, 0d, RelativeUnit.Relative));
        brushes[(int)Placement.Left] = CreateLinearBrush(sideStops,
            new RelativePoint(1d, 0d, RelativeUnit.Relative), new RelativePoint(0d, 0d, RelativeUnit.Relative));
        brushes[(int)Placement.Right] = CreateLinearBrush(sideStops,
            new RelativePoint(0d, 0d, RelativeUnit.Relative), new RelativePoint(1d, 0d, RelativeUnit.Relative));
        brushes[(int)Placement.Bottom] = CreateLinearBrush(sideStops,
            new RelativePoint(0d, 0d, RelativeUnit.Relative), new RelativePoint(0d, 1d, RelativeUnit.Relative));
        GradientStop[] topLeftStops;

        if (cornerRadius.TopLeft == 0d)
            topLeftStops = sideStops;
        else
            topLeftStops = CreateStops(c, cornerRadius.TopLeft);

        brushes[(int)Placement.TopLeft] =
            CreateRadialBrush(topLeftStops, new RelativePoint(1d, 1d, RelativeUnit.Relative));
        GradientStop[] topRightStops;

        if (cornerRadius.TopRight == 0d)
            topRightStops = sideStops;
        else if (cornerRadius.TopRight == cornerRadius.TopLeft)
            topRightStops = topLeftStops;
        else
            topRightStops = CreateStops(c, cornerRadius.TopRight);

        brushes[(int)Placement.TopRight] =
            CreateRadialBrush(topRightStops, new RelativePoint(0d, 1d, RelativeUnit.Relative));
        GradientStop[] bottomLeftStops;

        if (cornerRadius.BottomLeft == 0d)
            bottomLeftStops = sideStops;
        else if (cornerRadius.BottomLeft == cornerRadius.TopLeft)
            bottomLeftStops = topLeftStops;
        else if (cornerRadius.BottomLeft == cornerRadius.TopRight)
            bottomLeftStops = topRightStops;
        else
            bottomLeftStops = CreateStops(c, cornerRadius.BottomLeft);

        brushes[(int)Placement.BottomLeft] =
            CreateRadialBrush(bottomLeftStops, new RelativePoint(1d, 0d, RelativeUnit.Relative));
        GradientStop[] bottomRightStops;

        if (cornerRadius.BottomRight == 0d)
            bottomRightStops = sideStops;
        else if (cornerRadius.BottomRight == cornerRadius.TopLeft)
            bottomRightStops = topLeftStops;
        else if (cornerRadius.BottomRight == cornerRadius.TopRight)
            bottomRightStops = topRightStops;
        else if (cornerRadius.BottomRight == cornerRadius.BottomLeft)
            bottomRightStops = bottomLeftStops;
        else
            bottomRightStops = CreateStops(c, cornerRadius.BottomRight);

        brushes[(int)Placement.BottomRight] =
            CreateRadialBrush(bottomRightStops, new RelativePoint(0d, 0d, RelativeUnit.Relative));
        return brushes;
    }

    private static LinearGradientBrush CreateLinearBrush(GradientStop[] stops, RelativePoint startPoint,
        RelativePoint endPoint)
    {
        var brush = new LinearGradientBrush { StartPoint = startPoint, EndPoint = endPoint };
        AddStops(brush.GradientStops, stops);
        return brush;
    }

    private static RadialGradientBrush CreateRadialBrush(GradientStop[] stops, RelativePoint center)
    {
        var brush = new RadialGradientBrush
        {
            RadiusX = new RelativeScalar(1d, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(1d, RelativeUnit.Relative),
            Center = center,
            GradientOrigin = center
        };
        AddStops(brush.GradientStops, stops);
        return brush;
    }

    private static void AddStops(GradientStops target, GradientStop[] stops)
    {
        foreach (var stop in stops)
            target.Add(new GradientStop(stop.Color, stop.Offset));
    }

    private IBrush[] GetBrushes(Color c, CornerRadius cornerRadius)
    {
        if (_commonBrushes is null)
            lock (_resourceAccess)
            {
                if (_commonBrushes is null)
                {
                    _commonBrushes = CreateBrushes(c, cornerRadius);
                    _commonCornerRadius = cornerRadius;
                }
            }

        if (c == ((SolidColorBrush)_commonBrushes![(int)Placement.Center]).Color && cornerRadius == _commonCornerRadius)
        {
            _brushes = null;
            return _commonBrushes;
        }

        if (_brushes is null) _brushes = CreateBrushes(c, cornerRadius);

        return _brushes;
    }

    private enum Placement
    {
        TopLeft = 0,
        Top = 1,
        TopRight = 2,
        Left = 3,
        Center = 4,
        Right = 5,
        BottomLeft = 6,
        Bottom = 7,
        BottomRight = 8
    }
}
