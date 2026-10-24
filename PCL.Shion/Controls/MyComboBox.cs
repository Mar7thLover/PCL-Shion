using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Logging;
using PCL.Shion.Localization;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public class MyComboBox : ComboBox
{
    public delegate void TextChangedEventHandler(object? sender, RoutedEventArgs? e);

    public static readonly StyledProperty<string> HintTextProperty =
        AvaloniaProperty.Register<MyComboBox, string>(nameof(HintText), "");

    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<MyComboBox, bool>(nameof(IsEditable));

    private string? _Text;

    // 鼠标按下接口
    private bool isMouseDown;

    // 修复 WPF Bug：下拉框文本修改后，依然误认为还选择着此前的选项，导致再次点击该选项时内容不变
    private bool isTextChanging;
    private double realWidth; // 由于下拉框 Popup 宽度与 Width 一致，故不能为 NaN（Auto）
    private MyTextBox? textBox;
    private ContentPresenter? partContent;
    private Grid? panPopup;

    // 基础
    private static int uuidSeed;

    public int Uuid = GetUuid();

    public MyComboBox()
    {
        _Text = SelectedItem?.ToString() ?? "";
        AddHandler(PointerPressedEvent, MyComboBox_PreviewMouseLeftButtonDown, RoutingStrategies.Tunnel, true);
        AddHandler(PointerReleasedEvent, MyComboBox_PreviewMouseLeftButtonUp, RoutingStrategies.Tunnel, true);
        AddHandler(PointerReleasedEvent, (_, _) => RefreshColor(), RoutingStrategies.Tunnel, true);
        PointerExited += MyComboBox_PreviewMouseLeftButtonUp;
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        GotFocus += (_, _) => RefreshColor();
        TextChanged += MyComboBox_TextChanged;
    }

    public string HintText
    {
        get => GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    public bool IsEditable
    {
        get => GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public string Text
    {
        get
        {
            if (IsEditable)
            {
                if (textBox is null) return _Text ?? "";
                return textBox.Text ?? "";
            }

            return (SelectedItem ?? "").ToString() ?? "";
        }
        set
        {
            if (IsEditable)
            {
                if (textBox is null)
                    _Text = value;
                else
                    textBox.Text = value;
            }
            else
            {
                throw new NotSupportedException("该 ComboBox 不支持修改文本。");
            }
        }
    }

    public bool DropDownWidthSync { get; set; } = true;

    public ContentPresenter? ContentPresenter => partContent;
    public event TextChangedEventHandler? TextChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        partContent = e.NameScope.Find("PART_Content") as ContentPresenter;
        panPopup = e.NameScope.Find("PanPopup") as Grid;
        if (!IsEditable)
            return;
        try
        {
            textBox = (MyTextBox)e.NameScope.Find("PART_EditableTextBox")!;
            textBox.LostFocus += (_, _) => RefreshColor();
            textBox.changedEventList.Add((sender, args) => TextChanged?.Invoke(sender, args));
            textBox.Tag = Tag; // 有时需要用文本框的 Tag 来写入设置
            if (string.IsNullOrEmpty(Text))
                textBox.Text = _Text;
            else
                TextChanged?.Invoke(this, null);
            if (HintText.Length > 0)
                textBox.HintText = HintText;
            textBox.SetResourceReference(TextBox.CaretBrushProperty, "ColorBrushGray1");
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this,
                "初始化可编辑文本框失败（" + (Name ?? "") + "）：{Exception}", ex);
            Hint.Show(Lang.Text("Application.Control.Error.OperationFailed"), HintType.Error);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            RefreshColor();
        }
        else if (change.Property == IsDropDownOpenProperty)
        {
            if (IsDropDownOpen)
                MyComboBox_DropDownOpened(this, EventArgs.Empty);
            else
                MyComboBox_DropDownClosed(this, EventArgs.Empty);
        }
        else if (change.Property == HintTextProperty)
        {
            if (textBox is not null)
                textBox.HintText = HintText;
        }
    }

    private void MyComboBox_PreviewMouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        isMouseDown = true;
        RefreshColor();
    }

    private void MyComboBox_PreviewMouseLeftButtonUp(object? sender, EventArgs e)
    {
        isMouseDown = false;
    }

    // 指向动画
    public void RefreshColor()
    {
        // 判断当前颜色
        string foreColorName;
        string backColorName;
        int time;
        if (IsEnabled)
        {
            if (isMouseDown || IsDropDownOpen || (IsEditable && textBox?.IsFocused == true))
            {
                foreColorName = "ColorBrush3";
                backColorName = "ColorBrush7";
                time = 10;
            }
            else if (IsPointerOver)
            {
                foreColorName = "ColorBrush4";
                backColorName = "ColorBrush7";
                time = 100;
            }
            else
            {
                foreColorName = "ColorBrushBg0";
                backColorName = "ColorBrushHalfWhite";
                time = 100;
            }
        }
        else
        {
            foreColorName = "ColorBrushGray5";
            backColorName = "ColorBrushGray6";
            time = 200;
        }

        // 触发颜色动画
        if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
        {
            // 有动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, ForegroundProperty, foreColorName, time),
                    ModAnimation.AaColor(this, BackgroundProperty, backColorName, time)
                }, "MyComboBox Color " + Uuid);
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("MyComboBox Color " + Uuid);
            this.SetResourceReference(ForegroundProperty, foreColorName);
            this.SetResourceReference(BackgroundProperty, backColorName);
        }
    }

    private void MyComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        realWidth = Width;
        if (DropDownWidthSync)
            Width = Bounds.Width;
        try
        {
            var popup = panPopup ?? throw new InvalidOperationException("未找到下拉框容器 PanPopup。");
            if (MainWindow.Current is { } mainWindow)
                popup.Opacity = mainWindow.Opacity;
            if (!DropDownWidthSync)
                popup.MinWidth = Bounds.Width;
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "设置下拉框属性失败：{Exception}", ex);
            Hint.Show(Lang.Text("Application.Control.Error.OperationFailed"), HintType.Error);
        }
    }

    private void MyComboBox_DropDownClosed(object? sender, EventArgs e)
    {
        Width = realWidth;
    }

    private void MyComboBox_TextChanged(object? sender, RoutedEventArgs? e)
    {
        if (isTextChanging || !IsEditable)
            return;
        if (SelectedItem is null || Text == SelectedItem.ToString()) return;
        {
            var rawText = Text;
            var rawSelectionStart = textBox!.SelectionStart;
            isTextChanging = true;
            SelectedItem = null;
            Text = rawText;
            textBox.SelectionStart = rawSelectionStart;
            isTextChanging = false;
        }
    }

    // 用于 ItemsSource 的自定义容器
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        return new MyComboBoxItem();
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        if (item is MyComboBoxItem)
        {
            recycleKey = null;
            return false;
        }
        return base.NeedsContainerOverride(item, index, out recycleKey);
    }

    private static int GetUuid()
    {
        return Interlocked.Increment(ref uuidSeed);
    }
}
