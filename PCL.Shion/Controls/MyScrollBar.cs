using System;
using System.Diagnostics;
using System.Threading;
using Avalonia.Controls.Primitives;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public class MyScrollBar : ScrollBar
{
    // 基础

    private static int uuidSeed;

    public int Uuid = Interlocked.Increment(ref uuidSeed);

    private bool isMouseCaptureWithin;

    public MyScrollBar()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsEnabledProperty || e.Property == IsVisibleProperty)
                RefreshColor();
        };
        AddHandler(Thumb.DragStartedEvent, (_, _) => SetMouseCaptureWithin(true));
        AddHandler(Thumb.DragCompletedEvent, (_, _) => SetMouseCaptureWithin(false));
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
    }

    private void SetMouseCaptureWithin(bool value)
    {
        isMouseCaptureWithin = value;
        RefreshColor();
    }

    // 指向动画

    private void RefreshColor()
    {
        try
        {
            // 判断当前颜色
            double newOpacity;
            string newColor;
            int time;
            if (!IsVisible)
            {
                newOpacity = 0d;
                time = 20; // 防止错误的尺寸判断导致闪烁
                newColor = "ColorBrush4";
            }
            else if (isMouseCaptureWithin)
            {
                newOpacity = 1d;
                newColor = "ColorBrush4";
                time = 50;
            }
            else if (IsPointerOver)
            {
                newOpacity = 0.9d;
                newColor = "ColorBrush3";
                time = 130;
            }
            else
            {
                newOpacity = 0.5d;
                newColor = "ColorBrush4";
                time = 180;
            }

            // 触发颜色动画
            if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
            {
                // 有动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaColor(this, ForegroundProperty, newColor, time),
                        ModAnimation.AaOpacity(this, newOpacity - Opacity, time)
                    }, "MyScrollBar Color " + Uuid);
            }
            else
            {
                // 无动画
                ModAnimation.AniStop("MyScrollBar Color " + Uuid);
                this.SetResourceReference(ForegroundProperty, newColor);
                Opacity = newOpacity;
            }
        }

        catch (Exception ex)
        {
            Trace.TraceError("滚动条颜色改变出错：" + ex);
        }
    }
}
