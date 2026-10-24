using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Threading;
using PCL.Shion.Localization;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public partial class MyMsgAuthServer : Grid
{
    private readonly MsgBoxConverter _converter;
    private readonly int _uuid = MsgBox.GetUuid();

    // 与 WPF 版 XAML 中的 TransformGroup（RotateTransform + TranslateTransform）对应，改为代码创建
    private readonly RotateTransform TransformRotate = new(-4d);
    private readonly TranslateTransform TransformPos = new(0d, 40d);

    public MyMsgAuthServer(MsgBoxConverter converter)
    {
        InitializeComponent();
        RenderTransform = new TransformGroup { Children = { TransformRotate, TransformPos } };
        _converter = converter;
        LabTitle.Text = converter.Title;
        Btn1.Text = converter.Button1;
        Btn2.Text = converter.Button2;
        ComboServer.Text = converter.AuthServerDefault;
        foreach (var preset in converter.AuthServerPresets ?? new Dictionary<string, string>())
            ComboServer.Items.Add(new MyComboBoxItem { Content = preset.Key, Tag = preset.Value });
        ComboServer.SelectionChanged += (_, _) =>
        {
            if (ComboServer.SelectedItem is MyComboBoxItem item)
            {
                var value = item.Tag?.ToString() ?? string.Empty;
                Dispatcher.UIThread.Post(() =>
                {
                    ComboServer.SelectedItem = null;
                    ComboServer.Text = value;
                }, DispatcherPriority.Input);
            }
        };
        ShapeLine.StrokeThickness = 1d;
        LabTitle.PointerPressed += Drag;
        PanBorder.PointerPressed += Drag;
        Loaded += Load;
        Btn1.Click += Btn1_Click;
        Btn2.Click += Btn2_Click;
    }

    private void Load(object? sender, RoutedEventArgs e)
    {
        Opacity = 0d;
        MsgBox.AnimateBackgroundIn(false);
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaOpacity(this, 1d, 120, 60),
                ModAnimation.AaDouble(i => TransformPos.Y += (double)i, -TransformPos.Y, 300, 60,
                    new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i, -TransformRotate.Angle, 300, 60,
                    new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
            }, "MyMsgBox " + _uuid);
    }

    private void Btn1_Click(object? sender, PointerReleasedEventArgs e)
    {
        var server = ComboServer.Text?.Trim();
        if (string.IsNullOrWhiteSpace(server) ||
            !Uri.TryCreate(server, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Hint.Show(Lang.Text("Launch.Account.Auth.InvalidServer"), HintType.Error);
            return;
        }

        _converter.IsExited = true;
        _converter.Result = server;
        _Close();
    }

    private void Btn2_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (_converter.IsExited) return;
        _converter.IsExited = true;
        _converter.Result = null;
        _Close();
    }

    private void _Close()
    {
        _converter.Completion.TrySetResult(_converter.Result);
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
                6d - TransformRotate.Angle, 150, 0,
                new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() => (Parent as Panel)?.Children.Remove(this), after: true)
        }, "MyMsgBox " + _uuid);
    }

    private void Drag(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (e.GetCurrentPoint(ShapeLine).Properties.IsLeftButtonPressed && e.GetPosition(ShapeLine).Y <= 2d)
                MainWindow.Current?.BeginMoveDrag(e);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "拖拽移动失败：{Exception}", ex);
            Hint.Show(Lang.Text("Application.Control.MessageBox.Error.OperationFailed"), HintType.Error);
        }
    }
}
