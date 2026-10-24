using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Media;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public partial class MyMsgMarkdown : Grid
{
    private static readonly Regex HeadingPattern = new(@"^\s{0,3}(#{1,6})\s+(.*?)\s*#*\s*$", RegexOptions.Compiled);
    private static readonly Regex RulePattern = new(@"^\s{0,3}([-*_])(\s*\1){2,}\s*$", RegexOptions.Compiled);
    private static readonly Regex QuotePattern = new(@"^\s{0,3}>\s?(.*)$", RegexOptions.Compiled);
    private static readonly Regex BulletPattern = new(@"^\s*[-*+]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex OrderedPattern = new(@"^\s*(\d+)[.)]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex FencePattern = new(@"^\s*```", RegexOptions.Compiled);
    private static readonly Regex TableDividerPattern =
        new(@"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)+\|?\s*$", RegexOptions.Compiled);

    private static readonly Regex InlinePattern = new(
        @"\*\*(?<b>.+?)\*\*|__(?<b>.+?)__|\*(?<i>[^*\s][^*]*?)\*|`(?<c>[^`]+)`|\[(?<lt>[^\]]+)\]\((?<lu>[^)\s]+)\)",
        RegexOptions.Compiled);

    private readonly MsgBoxConverter myConverter;
    private readonly int uuid = MsgBox.GetUuid();

    // 与 WPF 版 XAML 中的 TransformGroup（RotateTransform + TranslateTransform）对应，改为代码创建
    private readonly RotateTransform TransformRotate = new(-4d);
    private readonly TranslateTransform TransformPos = new(0d, 40d);

    public MyMsgMarkdown(MsgBoxConverter converter)
    {
        try
        {
            InitializeComponent();
            RenderTransform = new TransformGroup { Children = { TransformRotate, TransformPos } };
            AppendUniqueNameSuffix(Btn1);
            AppendUniqueNameSuffix(Btn2);
            AppendUniqueNameSuffix(Btn3);
            myConverter = converter;
            LabTitle.Text = converter.Title;
            RenderMarkdown(converter.Text);
            ConfigurePrimaryButton(converter.Button1, converter.IsWarn);
            ConfigureSecondaryButton(Btn2, converter.Button2);
            ConfigureSecondaryButton(Btn3, converter.Button3);
            ShapeLine.StrokeThickness = 1d;
        }

        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "普通弹窗初始化失败：{Exception}", ex);
            Hint.Show(Lang.Text("Application.Control.MessageBox.Error.OperationFailed"), HintType.Error);
        }

        Loaded += Load;
        Btn1.Click += Btn1_Click;
        Btn2.Click += Btn2_Click;
        Btn3.Click += Btn3_Click;
    }

    private void AppendUniqueNameSuffix(Control element)
    {
        element.Name += MsgBox.GetUuid();
    }

    /// <summary>
    ///     将 Markdown 文本按块级元素渲染到 LabCaption 中：标题、段落、列表、引用、代码块、分割线与表格行。
    /// </summary>
    private void RenderMarkdown(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var paragraph = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (FencePattern.IsMatch(line))
            {
                AddParagraph(paragraph);
                var code = new List<string>();
                for (i++; i < lines.Length && !FencePattern.IsMatch(lines[i]); i++)
                    code.Add(lines[i]);
                LabCaption.Children.Add(CreateCodeBlock(string.Join("\n", code)));
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                AddParagraph(paragraph);
                continue;
            }

            var heading = HeadingPattern.Match(line);
            if (heading.Success)
            {
                AddParagraph(paragraph);
                LabCaption.Children.Add(CreateHeading(heading.Groups[2].Value, heading.Groups[1].Length));
                continue;
            }

            if (RulePattern.IsMatch(line))
            {
                AddParagraph(paragraph);
                LabCaption.Children.Add(CreateRule());
                continue;
            }

            var quote = QuotePattern.Match(line);
            if (quote.Success)
            {
                AddParagraph(paragraph);
                LabCaption.Children.Add(CreateText(quote.Groups[1].Value, 15d, italic: true));
                continue;
            }

            var bullet = BulletPattern.Match(line);
            if (bullet.Success)
            {
                AddParagraph(paragraph);
                LabCaption.Children.Add(CreateListItem("•", bullet.Groups[1].Value));
                continue;
            }

            var ordered = OrderedPattern.Match(line);
            if (ordered.Success)
            {
                AddParagraph(paragraph);
                LabCaption.Children.Add(CreateListItem(ordered.Groups[1].Value + ".", ordered.Groups[2].Value));
                continue;
            }

            if (line.TrimStart().StartsWith('|'))
            {
                if (TableDividerPattern.IsMatch(line))
                    continue;
                paragraph.Add(string.Join("  |  ", SplitTableRow(line)));
                continue;
            }

            paragraph.Add(line);
        }

        AddParagraph(paragraph);
    }

    private void AddParagraph(List<string> paragraph)
    {
        if (paragraph.Count == 0)
            return;
        LabCaption.Children.Add(CreateText(string.Join("\n", paragraph), 15d));
        paragraph.Clear();
    }

    private static List<string> SplitTableRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|'))
            trimmed = trimmed[1..];
        if (trimmed.EndsWith('|'))
            trimmed = trimmed[..^1];
        var cells = new List<string>();
        foreach (var cell in trimmed.Split('|'))
            cells.Add(cell.Trim());
        return cells;
    }

    private TextBlock CreateText(string text, double fontSize, FontWeight weight = FontWeight.Normal,
        bool italic = false)
    {
        var block = new TextBlock
        {
            FontSize = fontSize,
            FontWeight = weight,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top,
            Padding = new Thickness(1)
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                block.Inlines.Add(new LineBreak());
            AddInlines(block.Inlines, lines[i]);
        }

        return block;
    }

    private TextBlock CreateHeading(string text, int level)
    {
        var fontSize = level switch
        {
            1 => 20d,
            2 => 18d,
            3 => 17d,
            _ => 15d
        };
        var block = CreateText(text, fontSize, FontWeight.Bold);
        block.Margin = new Thickness(0, 6, 0, 0);
        return block;
    }

    private Grid CreateListItem(string marker, string text)
    {
        var markerBlock = new TextBlock
        {
            Text = marker,
            FontSize = 15d,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top
        };
        markerBlock.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
        var body = CreateText(text, 15d);
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 2, 0, 2)
        };
        grid.Children.Add(markerBlock);
        Grid.SetColumn(body, 1);
        grid.Children.Add(body);
        return grid;
    }

    private Border CreateRule()
    {
        var rule = new Border
        {
            Height = 1d,
            Margin = new Thickness(0, 8, 0, 8)
        };
        rule.SetResourceReference(Border.BackgroundProperty, "ColorBrushGray4");
        return rule;
    }

    private Border CreateCodeBlock(string code)
    {
        var block = new TextBlock
        {
            Text = code,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13d,
            TextWrapping = TextWrapping.Wrap
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
        var border = new Border
        {
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(4d),
            Margin = new Thickness(0, 4, 0, 4),
            Child = block
        };
        border.SetResourceReference(Border.BackgroundProperty, "ColorBrushHalfWhite");
        return border;
    }

    /// <summary>
    ///     解析行内标记：加粗、斜体、行内代码与链接。
    /// </summary>
    private void AddInlines(InlineCollection inlines, string text)
    {
        var last = 0;
        foreach (Match match in InlinePattern.Matches(text))
        {
            if (match.Index > last)
                inlines.Add(new Run(text[last..match.Index]));
            var groups = match.Groups;
            if (groups["b"].Success)
            {
                // 套在 Bold / Italic 里的 Run 不会显示为粗体、斜体，直接在 Run 上设置字重与字形
                inlines.Add(new Run(groups["b"].Value) { FontWeight = FontWeight.Bold });
            }
            else if (groups["i"].Success)
            {
                inlines.Add(new Run(groups["i"].Value) { FontStyle = FontStyle.Italic });
            }
            else if (groups["c"].Success)
            {
                inlines.Add(new Run(groups["c"].Value) { FontFamily = new FontFamily("Consolas") });
            }
            else if (groups["lt"].Success)
            {
                inlines.Add(new InlineUIContainer { Child = CreateLink(groups["lt"].Value, groups["lu"].Value) });
            }

            last = match.Index + match.Length;
        }

        if (last < text.Length)
            inlines.Add(new Run(text[last..]));
    }

    private HyperlinkButton CreateLink(string text, string url)
    {
        var link = new HyperlinkButton
        {
            Content = text,
            Padding = new Thickness(0),
            FontSize = 15d
        };
        link.SetResourceReference(HyperlinkButton.ForegroundProperty, "ColorBrush3");
        link.Click += (_, _) => OpenLink(url);
        return link;
    }

    private void OpenLink(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Logger.TryGet(LogEventLevel.Debug, LogArea.Control)?.Log(this, "[Control] 已忽略非 http(s) 的 Markdown 链接：" + url);
            return;
        }

        SystemShell.OpenUrl(url);
    }

    private void ConfigurePrimaryButton(string text, bool isWarn)
    {
        Btn1.Text = text;
        if (isWarn)
        {
            Btn1.ColorType = MyButton.ColorState.Red;
            MsgBox.SetWarnTitle(LabTitle);
        }
    }

    private static void ConfigureSecondaryButton(MyButton button, string text)
    {
        button.Text = text;
        button.IsVisible = !string.IsNullOrEmpty(text);
    }

    private void Load(object? sender, RoutedEventArgs e)
    {
        try
        {
            // UI 初始化
            if (Btn2.IsVisible && Btn1.ColorType != MyButton.ColorState.Red)
                Btn1.ColorType = MyButton.ColorState.Highlight;
            Btn1.Focus();
            // 动画
            Opacity = 0d;
            MsgBox.AnimateBackgroundIn(myConverter.IsWarn);
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaOpacity(this, 1d, 120, 60),
                    ModAnimation.AaDouble(i => TransformPos.Y += (double)i,
                        -TransformPos.Y, 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                        -TransformRotate.Angle, 300, 60,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                }, "MyMsgBox " + uuid);
            // 记录日志
            Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "[Control] 普通弹窗：" + LabTitle.Text);
        }

        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "普通弹窗加载失败：{Exception}", ex);
            Hint.Show(Lang.Text("Application.Control.MessageBox.Error.OperationFailed"), HintType.Error);
        }
    }

    private void Close()
    {
        // 结束线程阻塞
        myConverter.Completion.TrySetResult(myConverter.Result);
        // 动画
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                if (MsgBox.WaitingMyMsgBox.Count == 0)
                    MsgBox.AnimateBackgroundOut();
            }, 30),
            ModAnimation.AaOpacity(this, -Opacity, 80, 20),
            ModAnimation.AaDouble(i => TransformPos.Y += (double)i, 20d - TransformPos.Y,
                150, 0, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                6d - TransformRotate.Angle, 150, 0, new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() => (Parent as Panel)?.Children.Remove(this), after: true)
        }, "MyMsgBox " + uuid);
    }

    public void Btn1_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (myConverter.IsExited)
            return;
        if (myConverter.Button1Action is not null)
        {
            myConverter.Button1Action();
        }
        else
        {
            myConverter.IsExited = true;
            myConverter.Result = 1;
            Close();
        }
    }

    public void Btn2_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (myConverter.IsExited)
            return;
        if (myConverter.Button2Action is not null)
        {
            myConverter.Button2Action();
        }
        else
        {
            myConverter.IsExited = true;
            myConverter.Result = 2;
            Close();
        }
    }

    public void Btn3_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (myConverter.IsExited)
            return;
        if (myConverter.Button3Action is not null)
        {
            myConverter.Button3Action();
        }
        else
        {
            myConverter.IsExited = true;
            myConverter.Result = 3;
            Close();
        }
    }

    private void Drag(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (e.GetCurrentPoint(ShapeLine).Properties.IsLeftButtonPressed)
                if (e.GetPosition(ShapeLine).Y <= 2d)
                    MainWindow.Current?.BeginMoveDrag(e);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "拖拽移动失败：{Exception}", ex);
            Hint.Show(Lang.Text("Application.Control.MessageBox.Error.OperationFailed"), HintType.Error);
        }
    }
}
