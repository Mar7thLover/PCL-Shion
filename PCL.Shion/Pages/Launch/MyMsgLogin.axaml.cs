using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using PCL.Shion.Controls;
using PCL.Shion.Core.Accounts;
using PCL.Shion.Localization;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Launch;

/// <summary>
/// 设备码登录弹窗的数据：设备码信息、轮询方法与完成回调。
/// </summary>
public sealed class MyMsgLoginConverter
{
    /// <summary>设备码信息，包括需要输入的用户代码与验证网址。</summary>
    public required DeviceCodeInfo Device { get; init; }

    /// <summary>轮询一次授权结果；返回 null 表示没有结果。</summary>
    public required Func<CancellationToken, Task<TokenResponse?>> DeviceCodePoll { get; init; }

    /// <summary>完成时调用，参数为 <see cref="TokenResponse"/>（成功）、<see cref="OperationCanceledException"/>（取消）或 <see cref="Exception"/>（失败）。</summary>
    public Action<object?>? CompletionHandler { get; init; }

    public bool IsWarn { get; init; }

    /// <summary>弹窗是否已经关闭。</summary>
    public bool IsExited { get; set; }

    public object? Result { get; set; }
}

/// <summary>
/// 设备码登录弹窗：显示验证网址与用户代码，并在后台轮询授权结果，与 WPF 版 MyMsgLogin 对应。
/// </summary>
public partial class MyMsgLogin : Grid
{
    private readonly MyMsgLoginConverter _converter;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly int _uuid = MsgBox.GetUuid();
    private readonly string _userCode;
    private readonly string _website;

    // 对应 XAML 中 RenderTransform（TransformGroup）的两个子变换：先旋转，再平移
    private RotateTransform TransformRotate => (RotateTransform)((TransformGroup)RenderTransform!).Children[0];

    private TranslateTransform TransformPos => (TranslateTransform)((TransformGroup)RenderTransform!).Children[1];

    public MyMsgLogin(MyMsgLoginConverter converter)
    {
        InitializeComponent();
        _converter = converter;
        _userCode = converter.Device.UserCode;
        if (!string.IsNullOrEmpty(converter.Device.VerificationUriComplete))
        {
            _website = converter.Device.VerificationUriComplete;
            LabCaption.Text = Lang.Text("Launch.Account.LoginDialog.MicrosoftInstructions.WithAutoFill", _userCode, _website);
        }
        else
        {
            _website = converter.Device.VerificationUri;
            LabCaption.Text = Lang.Text("Launch.Account.LoginDialog.MicrosoftInstructions", _userCode, _website);
        }

        LabTitle.Text = Lang.Text("Launch.Account.LoginDialog.MinecraftLogin");
        Loaded += Load;
        Btn1.Click += (_, _) => OpenVerificationWebsite();
        Btn2.Click += (_, _) => ProfileUi.CopyText(_userCode);
        Btn3.Click += (_, _) => Cancel();
        LabTitle.PointerPressed += Drag;
        PanBorder.PointerPressed += Drag;
        _ = WorkThreadAsync();
    }

    /// <summary>主按钮和 Enter 共用的验证网页操作。</summary>
    public void OpenVerificationWebsite()
    {
        if (!_converter.IsExited)
            ProfileUi.OpenWebsite(_website);
    }

    /// <summary>取消按钮、Esc 和窗口关闭共用的结束操作；重复调用不会重复通知。</summary>
    public void Cancel() => Finished(new OperationCanceledException());

    /// <summary>结束登录：通知完成回调并关闭弹窗。</summary>
    private void Finished(object result)
    {
        if (_converter.IsExited)
            return;
        _converter.IsExited = true;
        _cancellation.Cancel();
        _converter.Result = result;
        _converter.CompletionHandler?.Invoke(result);
        Close();
        MainWindow.Current?.Activate();
    }

    /// <summary>等待两秒后打开网页并复制用户代码，然后按设备码规定的间隔轮询授权结果。</summary>
    private async Task WorkThreadAsync()
    {
        var token = _cancellation.Token;
        try
        {
            await Task.Delay(2000, token);
        }
        catch (OperationCanceledException) when (_converter.IsExited)
        {
            return;
        }

        if (_converter.IsExited)
            return;
        ProfileUi.OpenWebsite(_website);
        ProfileUi.CopyText(_userCode);
        var delayTime = TimeSpan.FromSeconds(_converter.Device.Interval);
        // 轮询
        var unknownFailureCount = 0;
        while (!_converter.IsExited && !token.IsCancellationRequested)
        {
            try
            {
                var resultJson = await _converter.DeviceCodePoll(token);
                token.ThrowIfCancellationRequested();
                if (resultJson is { IsError: true })
                {
                    switch (resultJson.Error)
                    {
                        case "authorization_pending":
                            await Task.Delay(delayTime, token);
                            continue;
                        case "slow_down":
                            delayTime += TimeSpan.FromSeconds(5);
                            await Task.Delay(delayTime, token);
                            continue;
                        case "expired_token":
                            Finished(new AccountException(resultJson.Error!,
                                resultJson.ErrorDescription ?? "设备授权已过期。"));
                            return;
                        case "access_denied":
                        case "authorization_declined":
                            Finished(new AccountException(resultJson.Error!,
                                resultJson.ErrorDescription ?? "用户拒绝了设备授权。"));
                            return;
                        default:
                            throw new AccountException(resultJson.Error ?? "invalid_response",
                                resultJson.ErrorDescription ?? "Unable to get body");
                    }
                }

                // 获取结果
                if (resultJson is null)
                    throw new InvalidDataException("微软令牌返回为空");
                ProfileUi.Info($"令牌过期时间：{resultJson.ExpiresIn} 秒");
                Hint.Show(Lang.Text("Launch.Account.LoginDialog.Success"), HintType.Success);
                Finished(resultJson);
                return;
            }
            catch (OperationCanceledException) when (_converter.IsExited)
            {
                return;
            }
            catch (Exception ex)
            {
                if (unknownFailureCount <= 2)
                {
                    unknownFailureCount += 1;
                    ProfileUi.Info($"正版验证轮询第 {unknownFailureCount} 次失败：{ex.Message}");
                    try
                    {
                        await Task.Delay(2000, token);
                    }
                    catch (OperationCanceledException) when (_converter.IsExited)
                    {
                        return;
                    }
                }
                else
                {
                    Finished(new InvalidOperationException(Lang.Text("Launch.Account.LoginDialog.PollingFailed"), ex));
                    return;
                }
            }
        }
    }

    private void Load(object? sender, RoutedEventArgs e)
    {
        Opacity = 0d;
        MsgBox.AnimateBackgroundIn(_converter.IsWarn);
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaOpacity(this, 1d, 120, 60),
                ModAnimation.AaDouble(i => TransformPos.Y += (double)i,
                    -TransformPos.Y, 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                    -TransformRotate.Angle, 300, 60,
                    new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
            }, "MyMsgBox " + _uuid);
    }

    private void Close()
    {
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
        if (e.GetCurrentPoint(ShapeLine).Properties.IsLeftButtonPressed && e.GetPosition(ShapeLine).Y <= 2d)
            MainWindow.Current?.BeginMoveDrag(e);
    }
}
