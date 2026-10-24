using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Threading;
using FluentValidation;
using PCL.Shion.Localization;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public class MyTextBox : TextBox
{
    public delegate void ValidateChangedEventHandler(object? sender, EventArgs e);

    public static readonly StyledProperty<string?> ValidateResultProperty =
        AvaloniaProperty.Register<MyTextBox, string?>(nameof(ValidateResult), "");

    private bool _isValidated = true;

    public static readonly DirectProperty<MyTextBox, bool> IsValidatedProperty =
        AvaloniaProperty.RegisterDirect<MyTextBox, bool>(nameof(IsValidated), o => o.IsValidated);

    public static readonly StyledProperty<string?> HintTextProperty =
        AvaloniaProperty.Register<MyTextBox, string?>(nameof(HintText), "");

    static MyTextBox()
    {
        CornerRadiusProperty.OverrideDefaultValue<MyTextBox>(new CornerRadius(3d));
        HintTextProperty.Changed.AddClassHandler<MyTextBox>((textBox, _) => textBox.UpdateHintText());
    }

    // 额外控件初始化

    public List<EventHandler<RoutedEventArgs>> changedEventList = new();

    // 提示文本

    /// <summary>
    ///     是否已经由用户输入过文本，若尚未输入过，则不显示输入检查的失败。
    /// </summary>
    private bool isTextChanged;

    private ValidateState shownValidateResult = ValidateState.NotInited;

    private static int _uuidSeed;

    private TextBlock? _labWrong;
    private TextBlock? _labHint;

    // 事件

    public int Uuid = GetUuid();

    public MyTextBox()
    {
        Loaded += (_, _) => Validate();
        TextChanged += (a, b) => MyTextBox_TextChanged((MyTextBox)a!, b);
        this.GetObservable(IsEnabledProperty).Subscribe(new Avalonia.Reactive.AnonymousObserver<bool>(_ => RefreshColor()));
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        GotFocus += (_, _) => RefreshColor();
        LostFocus += (_, _) => RefreshColor();
        this.GetObservable(IsEnabledProperty).Subscribe(new Avalonia.Reactive.AnonymousObserver<bool>(_ => RefreshTextColor()));
    }

    // 自定义属性

    public bool HasBackground { get; set; } = true;
    public bool ShowValidateResult { get; set; } = true;

    private TextBlock? labWrong => _labWrong;

    private TextBlock? labHint => _labHint;

    // 输入验证

    /// <summary>
    ///     输入验证结果。若为空字符串则无错误，否则为第一个错误原因。
    /// </summary>
    public string ValidateResult
    {
        get => GetValue(ValidateResultProperty) ?? "";
        set => SetValue(ValidateResultProperty, value);
    }

    /// <summary>
    ///     是否通过了输入验证。
    /// </summary>
    public bool IsValidated
    {
        get => _isValidated;
        private set => SetAndRaise(IsValidatedProperty, ref _isValidated, value);
    }

    /// <summary>
    ///     输入验证的规则。
    /// </summary>
    public Collection<IValidator<string>> ValidateRules
    {
        get => field;
        set
        {
            field = value;
            Validate();
        }
    } = new();

    public string HintText
    {
        get => GetValue(HintTextProperty) ?? "";
        set => SetValue(HintTextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValidateResultProperty)
            IsValidated = string.IsNullOrEmpty(change.GetNewValue<string?>());
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _labWrong = e.NameScope.Find<TextBlock>("labWrong");
        _labHint = e.NameScope.Find<TextBlock>("labHint");
        if (string.IsNullOrEmpty(HintText) || !string.IsNullOrEmpty(_labHint?.Text))
            return;
        UpdateHintText();
    }

    public event ValidateChangedEventHandler? ValidateChanged;

    public event EventHandler<RoutedEventArgs>? ValidatedTextChanged
    {
        add => changedEventList.Add(value!);
        remove => changedEventList.Remove(value!);
    }

    private void OnValidatedTextChanged(object? sender, RoutedEventArgs e)
    {
        foreach (var handler in changedEventList)
            if (handler is not null)
                handler.Invoke(sender, e);
    }

    /// <summary>
    ///     进行输入验证。
    /// </summary>
    public void Validate()
    {
        var stringResult = GetValidateResult();

        ValidateResult = stringResult;
        // 根据结果改变样式
        if (shownValidateResult != (IsValidated ? ValidateState.Success : ValidateState.FailedAndShowDetail))
        {
            if (IsLoaded && labWrong is not null)
                ChangeValidateResult(IsValidated, true);
            else
                RunInNewThread(() =>
                {
                    Thread.Sleep(30);
                    RunInUi(() => ChangeValidateResult(IsValidated, false));
                }, "DelayedValidate Change");
        }

        // 更新错误信息
        if (ShowValidateResult && !IsValidated)
        {
            if (IsLoaded && labWrong is not null)
                labWrong.Text = ValidateResult;
            else
                RunInNewThread(() =>
                {
                    var isFinished = false;
                    while (!isFinished)
                    {
                        Thread.Sleep(20);
                        RunInUiWait(() =>
                        {
                            if (labWrong is not null)
                            {
                                labWrong.Text = ValidateResult;
                                isFinished = true;
                            }

                            if (!IsLoaded)
                                isFinished = true;
                        });
                    }
                }, "DelayedValidate Text");
        }
    }

    /// <summary>
    ///     强制显示结果为正常，类似尚未输入过文本的状态。不影响实际的检查结果。
    /// </summary>
    public void ForceShowAsSuccess()
    {
        isTextChanged = false;
        ChangeValidateResult(IsValidated, true);
    }

    private void ChangeValidateResult(bool isSuccessful, bool isLoaded)
    {
        if (isLoaded && ModAnimation.AniControlEnabled == 0 && labWrong is not null)
        {
            if (isSuccessful || !isTextChanged)
            {
                // 变为正确
                shownValidateResult = isSuccessful ? ValidateState.Success : ValidateState.FailedButTextNotChanged;
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaOpacity(labWrong, -labWrong.Opacity, 150),
                        ModAnimation.AaHeight(labWrong, -labWrong.Height, 150,
                            ease: new ModAnimation.AniEaseOutFluent()),
                        ModAnimation.AaCode(() => labWrong.IsVisible = false, after: true)
                    }, "MyTextBox Validate " + Uuid);
            }
            else if (ShowValidateResult)
            {
                // 变为错误
                shownValidateResult = ValidateState.FailedAndShowDetail;
                labWrong.IsVisible = true;
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaOpacity(labWrong, 1d - labWrong.Opacity, 150),
                        ModAnimation.AaHeight(labWrong, 21d - labWrong.Height, 150,
                            ease: new ModAnimation.AniEaseOutFluent())
                    }, "MyTextBox Validate " + Uuid);
            }
            else
            {
                // 变为错误，但不显示文本
                shownValidateResult = ValidateState.FailedAndHideDetail;
            }
        }
        else
        {
            shownValidateResult = ValidateState.NotLoaded;
        }

        RefreshColor();
        ValidateChanged?.Invoke(this, new EventArgs());
    }

    private void MyTextBox_TextChanged(MyTextBox sender, RoutedEventArgs e)
    {
        try
        {
            UpdateHintText();
            // 改变输入记录
            isTextChanged = IsLoaded;
            // 进行输入验证
            Validate();
            if (!IsValidated)
                return;
            // 改变文本
            OnValidatedTextChanged(sender, e);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "进行输入验证时出错：{Exception}", ex);
            Hint.Show(Lang.Text("Application.Control.Error.OperationFailed"), HintType.Error);
        }
    }

    private void UpdateHintText()
    {
        if (labHint is null)
            return;
        labHint.Text = string.IsNullOrEmpty(Text) ? HintText : "";
    }

    private string GetValidateResult()
    {
        var stringResult = string.Empty;
        // 执行输入验证
        foreach (var rule in ValidateRules)
        {
            var result = rule.Validate(Text ?? "");
            stringResult = result.IsValid ? "" : result.Errors[0].ErrorMessage;
        }

        return stringResult;
    }

    // 颜色

    private void RefreshColor()
    {
        try
        {
            // 不对 ComboBox 从属进行动画
            if (TemplatedParent is not null && TemplatedParent is MyComboBox)
                return;
            // 判断当前颜色
            string foreColorName;
            string backColorName;
            int animationTime;
            if (IsEnabled)
            {
                if (IsValidated || !isTextChanged)
                {
                    if (IsFocused)
                    {
                        foreColorName = "ColorBrush3";
                        backColorName = "ColorBrush7";
                        animationTime = 10;
                    }
                    else if (IsPointerOver)
                    {
                        foreColorName = "ColorBrush4";
                        backColorName = "ColorBrush7";
                        animationTime = 100;
                    }
                    else // 未选中
                    {
                        foreColorName = "ColorBrushBg0";
                        backColorName = "ColorBrushHalfWhite";
                        animationTime = 100;
                    }
                }
                else
                {
                    foreColorName = "ColorBrushRedLight";
                    backColorName = "ColorBrushRedBack";
                    animationTime = 200;
                }
            }
            else
            {
                foreColorName = "ColorBrushGray5";
                backColorName = "ColorBrushGray6";
                animationTime = 200;
            }

            if (!HasBackground)
                backColorName = "ColorBrushTransparent";
            // 触发颜色动画
            if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
            {
                // 有动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaColor(this, BorderBrushProperty, foreColorName, animationTime),
                        ModAnimation.AaColor(this, BackgroundProperty, backColorName, animationTime)
                    }, "MyTextBox Color " + Uuid);
            }
            else
            {
                // 无动画
                ModAnimation.AniStop("MyTextBox Color " + Uuid);
                this.SetResourceReference(BorderBrushProperty, foreColorName);
                this.SetResourceReference(BackgroundProperty, backColorName);
            }
        }

        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "文本框颜色改变出错：{Exception}", ex);
        }
    }

    private void RefreshTextColor()
    {
        var resourceKey = IsEnabled ? "ColorBrushGray1" : "ColorBrushGray4";
        if (!this.TryFindResource(resourceKey, ActualThemeVariant, out var resource) ||
            resource is not ISolidColorBrush themeBrush)
            return;
        if (Foreground is ISolidColorBrush currentBrush && currentBrush.Color.R == themeBrush.Color.R)
            return;
        if (IsLoaded && ModAnimation.AniControlEnabled == 0 && !string.IsNullOrEmpty(Text))
        {
            // 有动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, ForegroundProperty, resourceKey, 200)
                }, "MyTextBox TextColor " + Uuid);
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("MyTextBox TextColor " + Uuid);
            Foreground = themeBrush;
        }
    }

    private static int GetUuid()
    {
        return Interlocked.Increment(ref _uuidSeed);
    }

    private static void RunInNewThread(Action action, string name)
    {
        new Thread(() =>
        {
            try
            {
                action();
            }
            catch (ThreadInterruptedException)
            {
                Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(null, name + "：线程已中止");
            }
            catch (Exception ex)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(null, name + "：线程执行失败：{Exception}", ex);
            }
        })
        {
            Name = name,
            IsBackground = true
        }.Start();
    }

    private static void RunInUi(Action action)
    {
        if (Application.Current is null)
            return;
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    private static void RunInUiWait(Action action)
    {
        if (Application.Current is null)
            return;
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Invoke(action);
    }

    private enum ValidateState
    {
        NotInited,
        Success,
        FailedButTextNotChanged,
        FailedAndShowDetail,
        FailedAndHideDetail,
        NotLoaded
    }
}
