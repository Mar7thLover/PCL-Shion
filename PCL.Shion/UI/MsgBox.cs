using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using FluentValidation;
using PCL.Shion.Controls;
using PCL.Shion.Localization;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.UI;

/// <summary>
///     存储弹窗信息的转换器。
/// </summary>
public sealed class MsgBoxConverter
{
    public string Button1 = "";

    /// <summary>
    ///     点击第一个按钮将执行该方法，不关闭弹窗。
    /// </summary>
    public Action? Button1Action;

    public string Button2 = "";

    /// <summary>
    ///     点击第二个按钮将执行该方法，不关闭弹窗。
    /// </summary>
    public Action? Button2Action;

    public string Button3 = "";

    /// <summary>
    ///     点击第三个按钮将执行该方法，不关闭弹窗。
    /// </summary>
    public Action? Button3Action;

    /// <summary>
    ///     输入模式：文本框的文本。
    ///     选择模式：需要放进去的列表。
    /// </summary>
    public object? Content;

    public bool ForceWait;

    /// <summary>
    ///     选择模式：是否允许勾选多个选项
    /// </summary>
    public bool MultiSelect;

    /// <summary>
    ///     有多个按钮时，是否给第一个按钮加高亮。
    /// </summary>
    public bool HighLight;

    /// <summary>
    ///     输入模式：提示文本。
    /// </summary>
    public string HintText = "";

    /// <summary>
    ///     弹窗是否已经关闭。
    /// </summary>
    public bool IsExited;

    public bool IsWarn;

    /// <summary>
    ///     输入模式：输入的文本。若点击了 非 第一个按钮，则为 null。
    ///     选择模式：点击的项的索引，从 0 开始。
    /// </summary>
    public object? Result;

    public string Text = "";
    public string Title = "";
    public MsgBoxType Type;

    /// <summary>
    ///     输入模式：输入验证规则。
    /// </summary>
    public Collection<IValidator<string>> ValidateRules { get; } = new();

    internal TaskCompletionSource<object?> Completion { get; } = new();

    public string AuthServerDefault = "";
    public IReadOnlyDictionary<string, string>? AuthServerPresets;
}

public enum MsgBoxType
{
    Text,
    Select,
    Input,
    AuthServer,
    Markdown
}

/// <summary>
///     弹窗队列与主窗口宿主管理。WPF 版为 ModMain.MyMsgBox 系列。
/// </summary>
public static class MsgBox
{
    private static int _uuid;

    private static DispatcherTimer? _tickTimer;

    /// <summary>
    ///     等待显示的弹窗。
    /// </summary>
    public static List<MsgBoxConverter> WaitingMyMsgBox { get; } = [];

    private static string GetDefaultDialogTitle() => Lang.Text("Common.Dialog.Title");

    private static string GetDefaultConfirmText() => Lang.Text("Common.Action.Confirm");

    private static string GetDefaultCancelText() => Lang.Text("Common.Action.Cancel");

    internal static int GetUuid() => Interlocked.Increment(ref _uuid);

    /// <summary>
    ///     显示弹窗，返回点击按钮的编号（从 1 开始）。
    /// </summary>
    /// <param name="caption">弹窗的内容。</param>
    /// <param name="title">弹窗的标题。</param>
    /// <param name="button1">显示的第一个按钮，默认为“确定”。</param>
    /// <param name="button2">显示的第二个按钮，默认为空。</param>
    /// <param name="button3">显示的第三个按钮，默认为空。</param>
    /// <param name="isWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static Task<int> ShowAsync(string caption, string? title = null, string? button1 = null,
        string? button2 = "", string? button3 = "", bool isWarn = false)
    {
        title ??= GetDefaultDialogTitle();
        button1 ??= GetDefaultConfirmText();
        button2 ??= "";
        button3 ??= "";
        // 将弹窗列入队列
        var converter = new MsgBoxConverter
        {
            Type = MsgBoxType.Text, Button1 = button1, Button2 = button2, Button3 = button3, Text = caption,
            IsWarn = isWarn, Title = title, HighLight = true, ForceWait = true
        };
        return EnqueueAsync<int>(converter);
    }

    /// <summary>
    ///     显示 Markdown 弹窗，返回点击按钮的编号（从 1 开始）。
    /// </summary>
    public static Task<int> ShowMarkdownAsync(string caption, string? title = null, string? button1 = null,
        string? button2 = "", string? button3 = "", bool isWarn = false)
    {
        title ??= GetDefaultDialogTitle();
        button1 ??= GetDefaultConfirmText();
        button2 ??= "";
        button3 ??= "";
        // 将弹窗列入队列
        var converter = new MsgBoxConverter
        {
            Type = MsgBoxType.Markdown, Button1 = button1, Button2 = button2, Button3 = button3, Text = caption,
            IsWarn = isWarn, Title = title, HighLight = true, ForceWait = true
        };
        return EnqueueAsync<int>(converter);
    }

    /// <summary>
    ///     显示输入框并返回输入的文本。若点击第二个按钮，则返回 null。
    /// </summary>
    /// <param name="title">弹窗的标题。</param>
    /// <param name="text">弹窗的介绍文本。</param>
    /// <param name="defaultInput">文本框的默认内容。</param>
    /// <param name="validate">文本框的输入检测，返回错误文本表示不合法。</param>
    public static Task<string?> InputAsync(string title, string text = "", string defaultInput = "",
        Func<string, string?>? validate = null)
    {
        // 将弹窗列入队列
        var converter = new MsgBoxConverter
        {
            Text = text, Type = MsgBoxType.Input,
            Button1 = GetDefaultConfirmText(), Button2 = GetDefaultCancelText(),
            Content = defaultInput, Title = title
        };
        if (validate is not null)
        {
            var validator = new InlineValidator<string>();
            validator.RuleFor(input => input).Custom((input, context) =>
            {
                var error = validate(input);
                if (error is not null)
                    context.AddFailure(error);
            });
            converter.ValidateRules.Add(validator);
        }

        return EnqueueAsync<string?>(converter);
    }

    /// <summary>
    ///     显示选择框并返回选择的第几项（从 0 开始）。若点击第二个按钮，则返回 null。
    /// </summary>
    /// <param name="selections">需要展示的选项。</param>
    /// <param name="title">弹窗的标题。</param>
    public static Task<int?> SelectAsync(IReadOnlyList<IMyRadio> selections, string? title = null)
    {
        title ??= GetDefaultDialogTitle();
        // 将弹窗列入队列
        var converter = new MsgBoxConverter
        {
            Type = MsgBoxType.Select, Button1 = GetDefaultConfirmText(), Button2 = "", Content = selections,
            Title = title
        };
        return EnqueueAsync<int?>(converter);
    }

    /// <summary>
    ///     显示多选选择框并返回勾选的所有项索引（从 0 开始）。若点击第二个按钮，则返回 null。
    /// </summary>
    public static Task<List<int>?> MultiSelectAsync(IReadOnlyList<MyListItem> selections, string? title = null)
    {
        title ??= GetDefaultDialogTitle();
        // 将弹窗列入队列
        var converter = new MsgBoxConverter
        {
            Type = MsgBoxType.Select, MultiSelect = true, Button1 = GetDefaultConfirmText(), Button2 = "",
            Content = selections, Title = title
        };
        return EnqueueAsync<List<int>?>(converter);
    }

    /// <summary>
    ///     显示验证服务器输入框并返回输入的地址。若点击第二个按钮，则返回 null。
    /// </summary>
    public static Task<string?> AuthServerAsync(string defaultServer, IReadOnlyDictionary<string, string> presets,
        string? title = null)
    {
        var converter = new MsgBoxConverter
        {
            Type = MsgBoxType.AuthServer,
            Title = title ?? Lang.Text("Launch.Account.Auth.SelectServer"),
            Button1 = GetDefaultConfirmText(),
            Button2 = GetDefaultCancelText(),
            AuthServerDefault = defaultServer,
            AuthServerPresets = presets,
            ForceWait = true
        };
        return EnqueueAsync<string?>(converter);
    }

    private static async Task<T> EnqueueAsync<T>(MsgBoxConverter converter)
    {
        if (Dispatcher.UIThread.CheckAccess())
            Enqueue(converter);
        else
            Dispatcher.UIThread.Post(() => Enqueue(converter));
        var result = await converter.Completion.Task;
        return (T)result!;
    }

    private static void Enqueue(MsgBoxConverter converter)
    {
        WaitingMyMsgBox.Add(converter);
        EnsureTicking();
        // 若为 UI 线程，立即执行弹窗刻，避免快速（连点器）点击时多次弹窗
        Tick();
    }

    private static void EnsureTicking()
    {
        if (_tickTimer is not null)
            return;
        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _tickTimer.Tick += (_, _) => Tick();
        _tickTimer.Start();
    }

    private static void StopTicking()
    {
        _tickTimer?.Stop();
        _tickTimer = null;
    }

    /// <summary>
    ///     将等待中的弹窗放入主窗口的弹窗宿主。WPF 版为 ModMain.MyMsgBoxTick。
    /// </summary>
    public static void Tick()
    {
        var window = MainWindow.Current;
        if (window?.PanMsg is null || window.WindowState == WindowState.Minimized)
            return;
        if (window.PanMsg.Children.Count > 0)
        {
            // 弹窗中
            if (window.PanMsgBackground is not null)
                window.PanMsgBackground.IsVisible = true;
        }
        else if (WaitingMyMsgBox.Count > 0)
        {
            // 没有弹窗，显示一个等待的弹窗
            if (window.PanMsgBackground is not null)
                window.PanMsgBackground.IsVisible = true;
            var converter = WaitingMyMsgBox[0];
            Control control = converter.Type switch
            {
                MsgBoxType.Input => new MyMsgInput(converter),
                MsgBoxType.Select => new MyMsgSelect(converter),
                MsgBoxType.AuthServer => new MyMsgAuthServer(converter),
                MsgBoxType.Markdown => new MyMsgMarkdown(converter),
                _ => new MyMsgText(converter)
            };
            window.PanMsg.Children.Add(control);
            WaitingMyMsgBox.RemoveAt(0);
        }
        // 没有弹窗，没有等待的弹窗
        else
        {
            if (window.PanMsgBackground is not null && window.PanMsgBackground.IsVisible)
                window.PanMsgBackground.IsVisible = false;
            StopTicking();
        }
    }

    /// <summary>
    ///     弹窗背景淡入。WPF 版为 MyMsg* 控件 Load 中的 AaColor。
    /// </summary>
    internal static void AnimateBackgroundIn(bool isWarn)
    {
        var background = MainWindow.Current?.PanMsgBackground;
        if (background is null)
            return;
        ModAnimation.AniStart(
            ModAnimation.AaColor(background, Border.BackgroundProperty,
                (isWarn
                    ? new MyColor(140d, 80d, 0d, 0d)
                    : new MyColor(90d, 0d, 0d, 0d)) - background.Background, 200),
            "PanMsgBackground Background");
    }

    /// <summary>
    ///     没有等待的弹窗时，淡出弹窗背景。
    /// </summary>
    internal static void AnimateBackgroundOut()
    {
        var background = MainWindow.Current?.PanMsgBackground;
        if (background is null)
            return;
        ModAnimation.AniStart(
            ModAnimation.AaColor(background, Border.BackgroundProperty,
                new MyColor(0d, 0d, 0d, 0d) - background.Background, 200,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
            "PanMsgBackground Background");
    }

    /// <summary>
    ///     设置标题为警告色（WPF 的 SetResourceReference("ColorBrushRedLight")）。
    /// </summary>
    internal static void SetWarnTitle(TextBlock title)
    {
        title.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedLight");
    }
}
