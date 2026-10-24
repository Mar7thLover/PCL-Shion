using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Logging;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public class MyComboBoxItem : ComboBoxItem
{
    // 指向动画

    private const int animationTimeIn = 100;
    private const int animationTimeOut = 300;
    private string? backColorName;
    private double fontOpacity;

    // 基础

    private static int uuidSeed;

    public int Uuid = GetUuid();

    public MyComboBoxItem()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsSelectedProperty || e.Property == IsEnabledProperty)
                RefreshColor();
        };
        PointerMoved += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        PointerReleased += MyComboBoxItem_PointerReleased;
    }

    private void RefreshColor()
    {
        // 判断当前颜色
        string newBackColorName;
        double newFontOpacity;
        int time;
        if (IsSelected)
        {
            newBackColorName = "ColorBrush6";
            newFontOpacity = 1d;
            time = animationTimeIn;
        }
        else if (IsPointerOver)
        {
            newBackColorName = "ColorBrush8";
            newFontOpacity = 1d;
            time = animationTimeIn;
        }
        else if (IsEnabled)
        {
            newBackColorName = "ColorBrushTransparent";
            newFontOpacity = 1d;
            time = animationTimeOut;
        }
        else
        {
            newBackColorName = "ColorBrushTransparent";
            newFontOpacity = 0.4d;
            time = animationTimeOut;
        }

        if ((backColorName ?? "") == (newBackColorName ?? "") && fontOpacity == newFontOpacity)
            return;
        backColorName = newBackColorName;
        fontOpacity = newFontOpacity;
        // 触发颜色动画
        if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
        {
            // 有动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, BackgroundProperty, backColorName, time),
                    ModAnimation.AaOpacity(this, fontOpacity - Opacity, time)
                }, "ComboBoxItem Color " + Uuid);
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("ComboBoxItem Color " + Uuid);
            this.SetResourceReference(BackgroundProperty, backColorName);
            Opacity = fontOpacity;
        }
    }

    private static int GetUuid()
    {
        return Interlocked.Increment(ref uuidSeed);
    }

    public override string ToString()
    {
        return Content?.ToString() ?? "";
    }

    public static implicit operator string(MyComboBoxItem value)
    {
        return value.Content?.ToString() ?? "";
    }

    private void MyComboBoxItem_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        Logger.TryGet(LogEventLevel.Debug, LogArea.Control)?.Log(this, "[Control] 选择下拉列表项：" + ToString());
    }
}
