using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace PCL.Shion.Controls;

/// <summary>把 SVG 路径数据（<c>d</c> 属性）转换为几何图形，支持 M L H V C S Q T A Z 及其相对写法。</summary>
internal static class SvgPathGeometryParser
{
    public static StreamGeometry Parse(string data, FillRule fillRule)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.SetFillRule(fillRule);
            new PathBuilder(data, context).Build();
        }

        return geometry;
    }

    private sealed class PathBuilder(string data, StreamGeometryContext context)
    {
        private int _index;
        private Point _current;
        private Point _figureStart;
        private Point? _lastCubicControl;
        private Point? _lastQuadraticControl;
        private bool _figureOpen;

        public void Build()
        {
            try
            {
                while (SkipSeparators() < data.Length)
                {
                    var command = data[_index];
                    if (!IsCommand(command))
                        throw new FormatException($"路径数据中出现意外字符“{command}”（位置 {_index}）。");

                    _index++;
                    Execute(command);
                }
            }
            finally
            {
                // 解析中途出错时也要收尾，保证几何上下文能正常释放
                EndFigure(false);
            }
        }

        private void Execute(char command)
        {
            var relative = char.IsLower(command);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    BeginFigure(ReadPoint(relative));
                    // 规范：M 之后多出的坐标对按 L 处理
                    while (HasNumber())
                        LineTo(ReadPoint(relative));
                    break;
                case 'L':
                    while (HasNumber())
                        LineTo(ReadPoint(relative));
                    break;
                case 'H':
                    while (HasNumber())
                    {
                        var x = ReadNumber();
                        LineTo(new Point(relative ? _current.X + x : x, _current.Y));
                    }

                    break;
                case 'V':
                    while (HasNumber())
                    {
                        var y = ReadNumber();
                        LineTo(new Point(_current.X, relative ? _current.Y + y : y));
                    }

                    break;
                case 'C':
                    while (HasNumber())
                        CubicTo(ReadPoint(relative), ReadPoint(relative), ReadPoint(relative));
                    break;
                case 'S':
                    while (HasNumber())
                    {
                        var control1 = _lastCubicControl is { } previous ? Reflect(previous, _current) : _current;
                        CubicTo(control1, ReadPoint(relative), ReadPoint(relative));
                    }

                    break;
                case 'Q':
                    while (HasNumber())
                        QuadraticTo(ReadPoint(relative), ReadPoint(relative));
                    break;
                case 'T':
                    while (HasNumber())
                    {
                        var control = _lastQuadraticControl is { } previous ? Reflect(previous, _current) : _current;
                        QuadraticTo(control, ReadPoint(relative));
                    }

                    break;
                case 'A':
                    while (HasNumber())
                        ArcTo(relative);
                    break;
                case 'Z':
                    ClosePath();
                    break;
            }
        }

        private void BeginFigure(Point point)
        {
            EndFigure(false);
            context.BeginFigure(point, true);
            _figureOpen = true;
            _current = point;
            _figureStart = point;
            ClearControls();
        }

        private void EndFigure(bool close)
        {
            if (!_figureOpen)
                return;

            context.EndFigure(close);
            _figureOpen = false;
        }

        // Z 之后若直接接绘图命令，新子路径从上一子路径的起点开始
        private void EnsureFigure()
        {
            if (!_figureOpen)
                BeginFigure(_current);
        }

        private void ClosePath()
        {
            EndFigure(true);
            _current = _figureStart;
            ClearControls();
        }

        private void LineTo(Point point)
        {
            EnsureFigure();
            context.LineTo(point, true);
            _current = point;
            ClearControls();
        }

        private void CubicTo(Point control1, Point control2, Point end)
        {
            EnsureFigure();
            context.CubicBezierTo(control1, control2, end, true);
            _current = end;
            _lastCubicControl = control2;
            _lastQuadraticControl = null;
        }

        private void QuadraticTo(Point control, Point end)
        {
            EnsureFigure();
            context.QuadraticBezierTo(control, end, true);
            _current = end;
            _lastQuadraticControl = control;
            _lastCubicControl = null;
        }

        private void ArcTo(bool relative)
        {
            var radiusX = Math.Abs(ReadNumber());
            var radiusY = Math.Abs(ReadNumber());
            var rotation = ReadNumber();
            var isLargeArc = ReadFlag();
            var isClockwise = ReadFlag();
            var end = ReadPoint(relative);

            EnsureFigure();
            if (radiusX <= 0D || radiusY <= 0D)
                context.LineTo(end, true);
            else
                context.ArcTo(
                    end,
                    new Size(radiusX, radiusY),
                    rotation,
                    isLargeArc,
                    isClockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise,
                    true);

            _current = end;
            ClearControls();
        }

        private void ClearControls()
        {
            _lastCubicControl = null;
            _lastQuadraticControl = null;
        }

        private static Point Reflect(Point point, Point around)
        {
            return new Point(around.X * 2D - point.X, around.Y * 2D - point.Y);
        }

        private Point ReadPoint(bool relative)
        {
            var x = ReadNumber();
            var y = ReadNumber();
            return relative ? new Point(_current.X + x, _current.Y + y) : new Point(x, y);
        }

        // 弧线的两个标志位只占一个字符，可以与后面的数字紧挨着写（如 "a1 1 0 011 1"）
        private bool ReadFlag()
        {
            SkipSeparators();
            if (_index < data.Length && data[_index] is '0' or '1')
                return data[_index++] == '1';

            throw new FormatException($"路径数据的弧线标志位无效（位置 {_index}）。");
        }

        private double ReadNumber()
        {
            SkipSeparators();
            var start = _index;

            if (_index < data.Length && data[_index] is '+' or '-')
                _index++;

            var hasDigit = SkipDigits();
            if (_index < data.Length && data[_index] == '.')
            {
                _index++;
                hasDigit |= SkipDigits();
            }

            if (!hasDigit)
                throw new FormatException($"路径数据中缺少数字（位置 {start}）。");

            // 仅当 e/E 后面确实跟着指数时才把它当作数字的一部分
            if (_index < data.Length && data[_index] is 'e' or 'E')
            {
                var exponentStart = _index++;
                if (_index < data.Length && data[_index] is '+' or '-')
                    _index++;

                if (!SkipDigits())
                    _index = exponentStart;
            }

            return double.Parse(data.AsSpan(start, _index - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private bool SkipDigits()
        {
            var start = _index;
            while (_index < data.Length && char.IsAsciiDigit(data[_index]))
                _index++;

            return _index > start;
        }

        private bool HasNumber()
        {
            return SkipSeparators() < data.Length && data[_index] is (>= '0' and <= '9') or '+' or '-' or '.';
        }

        private int SkipSeparators()
        {
            while (_index < data.Length && (char.IsWhiteSpace(data[_index]) || data[_index] == ','))
                _index++;

            return _index;
        }

        private static bool IsCommand(char ch)
        {
            return ch is 'M' or 'm' or 'L' or 'l' or 'H' or 'h' or 'V' or 'v'
                or 'C' or 'c' or 'S' or 's' or 'Q' or 'q' or 'T' or 't' or 'A' or 'a' or 'Z' or 'z';
        }
    }
}
