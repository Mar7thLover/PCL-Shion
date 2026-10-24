using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Shion.Controls;
using PCL.Shion.Navigation;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages;

public class MyPageLeft : Grid
{
    public static readonly StyledProperty<Control?> AnimatedControlProperty =
        AvaloniaProperty.Register<MyPageLeft, Control?>(nameof(AnimatedControl));

    private static int uuidSeed;
    private readonly int uuid = Interlocked.Increment(ref uuidSeed);

    /// <summary>
    ///     执行逐个进入动画的控件。
    /// </summary>
    public Control? AnimatedControl
    {
        get => GetValue(AnimatedControlProperty);
        set => SetValue(AnimatedControlProperty, value);
    }

    /// <summary>
    ///     次级页面的名称。主页面返回 null。
    /// </summary>
    public virtual string? Title => null;

    /// <summary>
    ///     导航到此页面时调用，<paramref name="sub" /> 为要显示的次级页面。
    /// </summary>
    public virtual void OnNavigated(PageSubType sub)
    {
    }

    public void TriggerShowAnimation()
    {
        if (AnimatedControl is null)
        {
            // 缩放动画
            if (RenderTransform is not ScaleTransform)
            {
                RenderTransform = new ScaleTransform(0.96d, 0.96d);
                RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
            }

            Opacity = 0d;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaScaleTransform(this, 1d - ((ScaleTransform)RenderTransform).ScaleX,
                        ease: new ModAnimation.AniEaseOutBack((ModAnimation.AniEasePower)2)),
                    ModAnimation.AaOpacity(this, 1d, 100)
                }, "PageLeft PageChange " + uuid);
        }
        else
        {
            // 逐个进入动画
            var aniList = new List<ModAnimation.AniData>();
            var id = 0;
            var delay = 0;
            foreach (var ElementRaw in GetAllAnimControls(true))
            {
                var element = MyVirtualizingElement.TryInit(ElementRaw);
                if (!element.IsVisible)
                {
                    // 还原之前的隐藏动画可能导致的改变（#2436）
                    element.Opacity = 1d;
                    element.RenderTransform = new TranslateTransform(0d, 0d);
                    if (element is MyListItem listItem)
                        listItem.isMouseOverAnimationEnabled = true;
                }
                else
                {
                    element.Opacity = 0d;
                    element.RenderTransform = new TranslateTransform(-25, 0d);
                    if (element is MyListItem listItem)
                        listItem.isMouseOverAnimationEnabled = false;
                    aniList.Add(ModAnimation.AaOpacity(element, element is TextBlock ? 0.6d : 1d, 100, delay,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
                    aniList.Add(ModAnimation.AaTranslateX(element, 5d, 200, delay,
                        new ModAnimation.AniEaseOutFluent()));
                    aniList.Add(ModAnimation.AaTranslateX(element, 20d, 300, delay,
                        new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)));
                    if (element is MyListItem item)
                        aniList.Add(ModAnimation.AaCode(() =>
                        {
                            item.isMouseOverAnimationEnabled = true;
                            item.RefreshColor(this, new EventArgs());
                        }, delay + 280));
                    delay += Math.Max(15 - id, 7) * 2;
                    id += 1;
                }
            }

            ModAnimation.AniStart(aniList, "PageLeft PageChange " + uuid);
        }
    }

    public void TriggerHideAnimation()
    {
        if (AnimatedControl is null)
        {
            // 缩放动画
            if (RenderTransform is not ScaleTransform)
            {
                RenderTransform = new ScaleTransform(1d, 1d);
                RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
            }

            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaScaleTransform(this, 0.95d - ((ScaleTransform)RenderTransform).ScaleX, 110,
                        ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaOpacity(this, -Opacity, 80, 30)
                }, "PageLeft PageChange " + uuid);
        }
        else
        {
            // 逐个退出动画
            var aniList = new List<ModAnimation.AniData>();
            var id = 0;
            var controls = GetAllAnimControls();
            foreach (var element in controls)
            {
                aniList.Add(ModAnimation.AaOpacity(element, -element.Opacity, 50,
                    (int)Math.Round(70d / controls.Count * id)));
                aniList.Add(ModAnimation.AaTranslateX(element, -6, 50, (int)Math.Round(70d / controls.Count * id)));
                id += 1;
            }

            ModAnimation.AniStart(aniList, "PageLeft PageChange " + uuid);
        }
    }

    // 遍历获取所有需要生成动画的控件
    private List<Control> GetAllAnimControls(bool ignoreInvisibility = false)
    {
        var allControls = new List<Control>();
        if (AnimatedControl is not null)
            GetAllAnimControls(AnimatedControl, ref allControls, ignoreInvisibility);
        return allControls;
    }

    private void GetAllAnimControls(Control element, ref List<Control> allControls, bool ignoreInvisibility)
    {
        if (!ignoreInvisibility && !element.IsVisible)
            return;
        if (element is MyTextButton)
            allControls.Add(element);
        else if (element is MyListItem)
            allControls.Add(element);
        else if (element is ContentControl contentControl)
        {
            if (contentControl.Content is Control content)
                GetAllAnimControls(content, ref allControls, ignoreInvisibility);
        }
        else if (element is Panel panel)
        {
            foreach (var element2 in panel.Children)
                GetAllAnimControls(element2, ref allControls, ignoreInvisibility);
        }
        else
        {
            allControls.Add(element);
        }
    }
}

public interface IRefreshable
{
    void Refresh();
}
