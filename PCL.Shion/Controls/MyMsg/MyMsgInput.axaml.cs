using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Media;
using PCL.Shion.Localization;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public partial class MyMsgInput : Grid
{
    private readonly MsgBoxConverter myConverter;
    private readonly int uuid = MsgBox.GetUuid();

    // 与 WPF 版 XAML 中的 TransformGroup（RotateTransform + TranslateTransform）对应，改为代码创建
    private readonly RotateTransform TransformRotate = new(-4d);
    private readonly TranslateTransform TransformPos = new(0d, 40d);

    public MyMsgInput(MsgBoxConverter converter)
    {
        try
        {
            InitializeComponent();
            RenderTransform = new TransformGroup { Children = { TransformRotate, TransformPos } };
            AppendUniqueNameSuffix(Btn1);
            AppendUniqueNameSuffix(Btn2);
            myConverter = converter;
            LabTitle.Text = converter.Title;
            LabText.Text = converter.Text;
            PanText.IsVisible = !string.IsNullOrEmpty(converter.Text);
            TextArea.Text = converter.Content as string ?? "";
            TextArea.HintText = converter.HintText;
            TextArea.ValidateRules = converter.ValidateRules;
            ConfigurePrimaryButton(converter.Button1, converter.IsWarn);
            ConfigureSecondaryButton(converter.Button2);
            ShapeLine.StrokeThickness = 1d;
        }

        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "输入弹窗初始化失败：{Exception}", ex);
            Hint.Show(Lang.Text("Application.Control.MessageBox.Error.OperationFailed"), HintType.Error);
        }

        Loaded += Load;
        Btn1.Click += Btn1_Click;
        Btn2.Click += Btn2_Click;
    }

    private void AppendUniqueNameSuffix(Control element)
    {
        element.Name += MsgBox.GetUuid();
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

    private void ConfigureSecondaryButton(string text)
    {
        Btn2.Text = text;
        Btn2.IsVisible = !string.IsNullOrEmpty(text);
    }

    private void Load(object? sender, RoutedEventArgs e)
    {
        try
        {
            // UI 初始化
            if (Btn2.IsVisible && Btn1.ColorType != MyButton.ColorState.Red)
                Btn1.ColorType = MyButton.ColorState.Highlight;
            TextArea.Focus();
            TextArea.CaretIndex = TextArea.Text?.Length ?? 0;
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
            Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "[Control] 输入弹窗：" + LabTitle.Text);
        }

        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "输入弹窗加载失败：{Exception}", ex);
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
        TextArea.Validate(); // #5773
        if (myConverter.IsExited || !TextArea.IsValidated)
            return;
        myConverter.IsExited = true;
        myConverter.Result = TextArea.Text;
        Close();
    }

    public void Btn2_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (myConverter.IsExited)
            return;
        myConverter.IsExited = true;
        myConverter.Result = null;
        Close();
    }

    private void TextCaption_ValidateChanged(object? sender, EventArgs e)
    {
        Btn1.IsEnabled = TextArea.IsValidated;
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
