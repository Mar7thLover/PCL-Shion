using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Media;
using PCL.Shion.Localization;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public partial class MyMsgSelect : Grid
{
    private readonly MsgBoxConverter myConverter;
    private readonly int uuid = MsgBox.GetUuid();

    private int selectedIndex = -1;

    // 与 WPF 版 XAML 中的 TransformGroup（RotateTransform + TranslateTransform）对应，改为代码创建
    private readonly RotateTransform TransformRotate = new(-4d);
    private readonly TranslateTransform TransformPos = new(0d, 40d);

    public MyMsgSelect(MsgBoxConverter converter)
    {
        try
        {
            InitializeComponent();
            RenderTransform = new TransformGroup { Children = { TransformRotate, TransformPos } };
            AppendUniqueNameSuffix(Btn1);
            AppendUniqueNameSuffix(Btn2);
            myConverter = converter;
            LabTitle.Text = converter.Title;
            ConfigurePrimaryButton(converter.Button1, converter.IsWarn);
            ConfigureSecondaryButton(converter.Button2);
            ShapeLine.StrokeThickness = 1d;
            InitializeSelectionList(converter.Content!);
        }

        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "选择弹窗初始化失败：{Exception}", ex);
            Hint.Show(Lang.Text("Application.Control.MessageBox.Error.OperationFailed"), HintType.Error);
        }

        Loaded += Load;
        Btn1.Click += Btn1_Click;
        Btn2.Click += Btn2_Click;
        LabTitle.PointerPressed += Drag;
        PanBorder.PointerPressed += Drag;
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

    private void InitializeSelectionList(object content)
    {
        // 添加选择控件
        Btn1.IsEnabled = false;
        foreach (var rawContent in (IEnumerable)content)
        {
            // 1. 初始化并获取实际元素
            var selectionContent = MyVirtualizingElement.TryInit((Control)rawContent);

            // 2. 接口转换与事件订阅
            if (selectionContent is IMyRadio selection)
            {
                PanSelection.Children.Add((Control)selection);
                selection.Check += (sender, e) => OnChecked((IMyRadio)sender, e);
                selection.Changed += (_, _) => _UpdateCheckedState();

                // 3. 根据具体类型配置属性
                if (selection is MyListItem listItem)
                {
                    listItem.Type =
                        myConverter.MultiSelect ? MyListItem.CheckType.CheckBox : MyListItem.CheckType.RadioBox;
                    listItem.MinHeight = 24d;
                }
                else if (selection is MyRadioBox radioBox)
                {
                    radioBox.MinHeight = 24d;
                }
            }
        }
    }

    private void Load(object? sender, RoutedEventArgs e)
    {
        try
        {
            // UI 初始化
            if (Btn2.IsVisible && Btn1.ColorType != MyButton.ColorState.Red)
                Btn1.ColorType = MyButton.ColorState.Highlight;
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
            Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "[Control] 选择弹窗：" + LabTitle.Text);
        }

        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "选择弹窗加载失败：{Exception}", ex);
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
        if (myConverter.MultiSelect)
        {
            var indices = new List<int>();
            for (var i = 0; i < PanSelection.Children.Count; i++)
                if (PanSelection.Children[i] is MyListItem { Checked: true })
                    indices.Add(i);
            if (indices.Count == 0)
                return;
            myConverter.IsExited = true;
            myConverter.Result = indices;
        }
        else
        {
            if (selectedIndex == -1)
                return;
            myConverter.IsExited = true;
            myConverter.Result = selectedIndex;
        }

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

    private void OnChecked(IMyRadio sender, EventArgs e)
    {
        if (myConverter.MultiSelect)
            return;
        Btn1.IsEnabled = true;
        selectedIndex = PanSelection.Children.IndexOf((Control)sender);
    }

    private void _UpdateCheckedState()
    {
        // 多选
        if (!myConverter.MultiSelect)
            return;
        var checkedCount = 0;
        foreach (var child in PanSelection.Children)
            if (child is MyListItem { Checked: true })
                checkedCount += 1;
        Btn1.IsEnabled = checkedCount > 0;
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
