using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;

namespace PCL.Shion.Controls;

internal static class SvgIconParser
{
    public static SvgIconModel Parse(string svg)
    {
        using var stringReader = new StringReader(svg);
        using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });

        var document = XDocument.Load(xmlReader, LoadOptions.None);
        var root = document.Root ?? throw new FormatException("SVG 文件缺少根节点。");

        var (minX, minY, width, height) = ReadViewBox(root);
        var elements = new List<SvgIconElement>();

        ReadElements(root, elements, new SvgIconStyle());

        return new SvgIconModel
        {
            MinX = minX,
            MinY = minY,
            Width = width <= 0D ? 24D : width,
            Height = height <= 0D ? 24D : height,
            Elements = elements
        };
    }

    private static void ReadElements(
        XElement parent,
        ICollection<SvgIconElement> result,
        SvgIconStyle inheritedStyle)
    {
        var parentStyle = inheritedStyle.Merge(parent);

        foreach (var element in parent.Elements())
        {
            var name = element.Name.LocalName;

            if (name is "g" or "svg")
            {
                ReadElements(element, result, parentStyle);
                continue;
            }

            var iconElement = CreateElement(name, element, parentStyle.Merge(element));
            if (iconElement is not null)
                result.Add(iconElement);
        }
    }

    private static SvgIconElement? CreateElement(string name, XElement element, SvgIconStyle style)
    {
        try
        {
            return name switch
            {
                "path" => CreatePath(element, style),
                "line" => CreateLine(element, style),
                "circle" => CreateCircle(element, style),
                "ellipse" => CreateEllipse(element, style),
                "rect" => CreateRect(element, style),
                "polyline" => CreatePoints(element, style, SvgIconElementKind.Polyline),
                "polygon" => CreatePoints(element, style, SvgIconElementKind.Polygon),
                _ => null
            };
        }
        catch (Exception)
        {
            // 单个图元解析失败时跳过，避免一个不兼容节点导致整个图标不可用
            return null;
        }
    }

    private static SvgIconElement? CreatePath(XElement element, SvgIconStyle style)
    {
        var d = Attr(element, "d");
        if (string.IsNullOrWhiteSpace(d))
            return null;

        return new SvgIconElement(SvgIconElementKind.Path, SvgPathGeometryParser.Parse(d, ParseFillRule(style)), style);
    }

    private static SvgIconElement CreateLine(XElement element, SvgIconStyle style)
    {
        var geometry = new LineGeometry(
            new Point(Number(element, "x1"), Number(element, "y1")),
            new Point(Number(element, "x2"), Number(element, "y2")));

        return new SvgIconElement(SvgIconElementKind.Line, geometry, style);
    }

    private static SvgIconElement CreateCircle(XElement element, SvgIconStyle style)
    {
        var radius = Number(element, "r");
        var geometry = new EllipseGeometry(
            new Rect(Number(element, "cx") - radius, Number(element, "cy") - radius, radius * 2D, radius * 2D));

        return new SvgIconElement(SvgIconElementKind.Circle, geometry, style);
    }

    private static SvgIconElement CreateEllipse(XElement element, SvgIconStyle style)
    {
        var radiusX = Number(element, "rx");
        var radiusY = Number(element, "ry");
        var geometry = new EllipseGeometry(
            new Rect(Number(element, "cx") - radiusX, Number(element, "cy") - radiusY, radiusX * 2D, radiusY * 2D));

        return new SvgIconElement(SvgIconElementKind.Ellipse, geometry, style);
    }

    private static SvgIconElement CreateRect(XElement element, SvgIconStyle style)
    {
        var rect = new Rect(Number(element, "x"), Number(element, "y"), Number(element, "width"), Number(element, "height"));
        var radiusX = Number(element, "rx", Number(element, "ry"));
        var radiusY = Number(element, "ry", radiusX);

        return new SvgIconElement(SvgIconElementKind.Rect, new RectangleGeometry(rect, radiusX, radiusY), style);
    }

    private static SvgIconElement? CreatePoints(XElement element, SvgIconStyle style, SvgIconElementKind kind)
    {
        var numbers = SvgNumberParser.ParseNumberList(Attr(element, "points"));
        if (numbers.Length < 4)
            return null;

        var close = kind == SvgIconElementKind.Polygon;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.SetFillRule(close ? FillRule.NonZero : FillRule.EvenOdd);
            context.BeginFigure(new Point(numbers[0], numbers[1]), close);
            for (var i = 2; i + 1 < numbers.Length; i += 2)
                context.LineTo(new Point(numbers[i], numbers[i + 1]), true);
            context.EndFigure(close);
        }

        return new SvgIconElement(kind, geometry, style);
    }

    private static (double MinX, double MinY, double Width, double Height) ReadViewBox(XElement root)
    {
        var numbers = SvgNumberParser.ParseNumberList(Attr(root, "viewBox"));
        if (numbers.Length == 4)
            return (numbers[0], numbers[1], numbers[2], numbers[3]);

        var width = SvgNumberParser.TryParseNullable(Attr(root, "width")) ?? 24D;
        var height = SvgNumberParser.TryParseNullable(Attr(root, "height")) ?? 24D;
        return (0D, 0D, width, height);
    }

    private static FillRule ParseFillRule(SvgIconStyle style)
    {
        return string.Equals(style.FillRule?.Trim(), "evenodd", StringComparison.OrdinalIgnoreCase)
            ? FillRule.EvenOdd
            : FillRule.NonZero;
    }

    private static double Number(XElement element, string name, double fallback = 0D)
    {
        return SvgNumberParser.TryParse(Attr(element, name), fallback);
    }

    private static string? Attr(XElement element, string name)
    {
        return element.Attribute(name)?.Value;
    }
}
