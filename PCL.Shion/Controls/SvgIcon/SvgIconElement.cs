using System;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace PCL.Shion.Controls;

internal sealed class SvgIconElement
{
    private const double FullOpacity = 0.9999D;

    private readonly bool _hasFill;
    private readonly bool _hasStroke;
    private readonly bool _noFill;
    private readonly bool _noStroke;
    private readonly IBrush? _fillColor;
    private readonly IBrush? _strokeColor;
    private readonly PenLineCap _lineCap;
    private readonly PenLineJoin _lineJoin;

    public SvgIconElement(SvgIconElementKind kind, Geometry geometry, SvgIconStyle style)
    {
        Kind = kind;
        Geometry = geometry;
        Style = style;

        _noFill = IsNone(style.Fill);
        _noStroke = IsNone(style.Stroke);
        _hasFill = HasPaint(style.Fill);
        _hasStroke = HasPaint(style.Stroke);
        _fillColor = _hasFill ? SvgPaintParser.ParseColorBrush(style.Fill) : null;
        _strokeColor = _hasStroke ? SvgPaintParser.ParseColorBrush(style.Stroke) : null;
        _lineCap = ParseLineCap(style.StrokeLineCap);
        _lineJoin = ParseLineJoin(style.StrokeLineJoin);
    }

    public SvgIconElementKind Kind { get; }
    public Geometry Geometry { get; }
    public SvgIconStyle Style { get; }

    public bool PreferStrokeByDefault => Kind is SvgIconElementKind.Line or SvgIconElementKind.Polyline;

    public void Draw(DrawingContext context, SvgIconPaintOptions options)
    {
        if (Style.Opacity <= 0D)
            return;

        var fill = ResolveFill(options);
        if (fill is not null)
            Paint(context, fill, Style.Opacity * Style.FillOpacity, 0D);

        // SVG 自带的 stroke-width 优先，StrokeThickness 只在 SVG 没有指定时生效
        var thickness = Style.StrokeWidth ?? options.StrokeThickness;
        var stroke = ResolveStroke(options);
        if (stroke is not null && thickness > 0D)
            Paint(context, stroke, Style.Opacity * Style.StrokeOpacity, thickness);
    }

    private IBrush? ResolveFill(SvgIconPaintOptions options)
    {
        if (_noFill)
            return null;

        if (!options.UseOriginalColor)
            return !_hasFill && (_hasStroke || PreferStrokeByDefault) ? null : options.IconBrush;

        if (_hasFill)
            return _fillColor ?? options.IconBrush;

        return !_hasStroke && !PreferStrokeByDefault ? Brushes.Black : null;
    }

    private IBrush? ResolveStroke(SvgIconPaintOptions options)
    {
        if (_noStroke)
            return null;

        if (!options.UseOriginalColor)
            return _hasStroke || PreferStrokeByDefault ? options.IconBrush : null;

        if (_hasStroke)
            return _strokeColor ?? options.IconBrush;

        return PreferStrokeByDefault ? Brushes.Black : null;
    }

    /// <summary>用给定画刷填充（thickness 为 0）或按线宽描边，并叠加不透明度。</summary>
    private void Paint(DrawingContext context, IBrush brush, double opacity, double thickness)
    {
        opacity = Math.Clamp(opacity, 0D, 1D);
        if (opacity <= 0D)
            return;

        if (opacity > FullOpacity)
        {
            Submit(context, brush, thickness);
        }
        else if (brush is ISolidColorBrush solid)
        {
            Submit(context, new ImmutableSolidColorBrush(solid.Color, solid.Opacity * opacity), thickness);
        }
        else
        {
            using (context.PushOpacity(opacity))
                Submit(context, brush, thickness);
        }
    }

    private void Submit(DrawingContext context, IBrush brush, double thickness)
    {
        if (thickness <= 0D)
        {
            context.DrawGeometry(brush, null, Geometry);
            return;
        }

        var pen = new Pen(brush, thickness)
        {
            LineCap = _lineCap,
            LineJoin = _lineJoin
        };
        context.DrawGeometry(null, pen, Geometry);
    }

    private static bool HasPaint(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && !IsNone(value);
    }

    private static bool IsNone(string? value)
    {
        return string.Equals(value?.Trim(), "none", StringComparison.OrdinalIgnoreCase);
    }

    private static PenLineCap ParseLineCap(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "butt" => PenLineCap.Flat,
            "square" => PenLineCap.Square,
            _ => PenLineCap.Round
        };
    }

    private static PenLineJoin ParseLineJoin(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "miter" => PenLineJoin.Miter,
            "bevel" => PenLineJoin.Bevel,
            _ => PenLineJoin.Round
        };
    }
}
