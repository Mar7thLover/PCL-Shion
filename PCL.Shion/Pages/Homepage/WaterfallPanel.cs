using System;
using Avalonia;
using Avalonia.Controls;

namespace PCL.Shion.Pages.Homepage;

/// <summary>
/// 瀑布流面板：按 <see cref="MaxItemWidth"/> 自动计算列数，每个子元素放进当前最矮的一列。
/// </summary>
public sealed class WaterfallPanel : Panel
{
    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<WaterfallPanel, double>(nameof(ColumnSpacing), 5);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<WaterfallPanel, double>(nameof(RowSpacing));

    public static readonly StyledProperty<double> MaxItemWidthProperty =
        AvaloniaProperty.Register<WaterfallPanel, double>(nameof(MaxItemWidth), 100);

    private int _columnCount = 1;

    static WaterfallPanel()
    {
        AffectsMeasure<WaterfallPanel>(ColumnSpacingProperty, RowSpacingProperty, MaxItemWidthProperty);
    }

    /// <summary>列间距。</summary>
    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>行间距。</summary>
    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>每项的最大宽度。</summary>
    public double MaxItemWidth
    {
        get => GetValue(MaxItemWidthProperty);
        set => SetValue(MaxItemWidthProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var unbounded = double.IsInfinity(width);
        _columnCount = unbounded
            ? 1
            : Math.Max(1, (int)Math.Floor((width + ColumnSpacing) / (MaxItemWidth + ColumnSpacing)));
        var columnWidth = unbounded ? width : GetColumnWidth(width);

        var heights = new double[_columnCount];
        var widest = 0d;
        foreach (var child in Children)
        {
            child.Measure(new Size(columnWidth, double.PositiveInfinity));
            heights[ShortestColumn(heights)] += child.DesiredSize.Height + RowSpacing;
            widest = Math.Max(widest, child.DesiredSize.Width);
        }

        // 每列末尾多算了一个行间距
        var height = 0d;
        foreach (var columnHeight in heights)
        {
            if (columnHeight > 0)
                height = Math.Max(height, columnHeight - RowSpacing);
        }

        return new Size(unbounded ? widest : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columnWidth = GetColumnWidth(finalSize.Width);
        var heights = new double[_columnCount];
        foreach (var child in Children)
        {
            var column = ShortestColumn(heights);
            child.Arrange(new Rect(column * (columnWidth + ColumnSpacing), heights[column], columnWidth,
                child.DesiredSize.Height));
            heights[column] += child.DesiredSize.Height + RowSpacing;
        }

        return finalSize;
    }

    private double GetColumnWidth(double totalWidth) =>
        Math.Max(0, (totalWidth - (_columnCount - 1) * ColumnSpacing) / _columnCount);

    private static int ShortestColumn(double[] heights)
    {
        var shortest = 0;
        for (var i = 1; i < heights.Length; i++)
        {
            if (heights[i] < heights[shortest])
                shortest = i;
        }

        return shortest;
    }
}
