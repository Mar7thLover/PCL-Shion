using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

internal static class SvgIconControlHelper
{
    internal static bool HasSvgIcon(string? icon)
    {
        return !string.IsNullOrWhiteSpace(icon);
    }

    internal static void ApplyVisibility(
        Path legacyIcon,
        SvgIcon svgIcon,
        bool useSvgIcon)
    {
        legacyIcon.IsVisible = !useSvgIcon;
        svgIcon.IsVisible = useSvgIcon;
    }

    internal static void ApplyIcon(Path legacyIcon, SvgIcon svgIcon, string? svgIconName)
    {
        var useSvgIcon = HasSvgIcon(svgIconName);
        svgIcon.Icon = svgIconName ?? string.Empty;
        ApplyVisibility(legacyIcon, svgIcon, useSvgIcon);
    }

    internal static void SetIconBrush(Path legacyIcon, SvgIcon svgIcon, bool useSvgIcon, IBrush brush)
    {
        if (useSvgIcon)
            ResourceReferenceExtensions.SetValueReplacingBinding(svgIcon, SvgIcon.IconBrushProperty, brush);
        else
            ResourceReferenceExtensions.SetValueReplacingBinding(legacyIcon, Shape.FillProperty, brush);
    }

    internal static void SetIconResource(Path legacyIcon, SvgIcon svgIcon, bool useSvgIcon, string resourceKey)
    {
        if (useSvgIcon)
            svgIcon.SetResourceReference(SvgIcon.IconBrushProperty, resourceKey);
        else
            legacyIcon.SetResourceReference(Shape.FillProperty, resourceKey);
    }

    internal static void AnimateSvgIconBrushTo(
        SvgIcon svgIcon,
        string resourceKey,
        int duration,
        string? animationKey = null)
    {
        if (svgIcon.IsVisible)
            svgIcon.AnimateIconBrushTo(
                ResolveResourceColor(resourceKey),
                TimeSpan.FromMilliseconds(duration),
                animationKey: animationKey);
    }

    internal static void AnimateSvgIconBrushTo(
        SvgIcon svgIcon,
        MyColor color,
        int duration,
        string? animationKey = null)
    {
        if (svgIcon.IsVisible)
            svgIcon.AnimateIconBrushTo(
                color,
                TimeSpan.FromMilliseconds(duration),
                animationKey: animationKey);
    }

    private static MyColor ResolveResourceColor(string resourceKey)
    {
        if (Application.Current is not { } app || !app.TryFindResource(resourceKey, out var resource))
            return ParseColor(resourceKey);

        return resource switch
        {
            ISolidColorBrush brush => new MyColor(brush.Color),
            Color color => new MyColor(color),
            _ => ParseColor(resourceKey)
        };
    }

    private static MyColor ParseColor(string value)
    {
        try
        {
            return new MyColor(value);
        }
        catch (Exception)
        {
            return new MyColor(0d, 0d, 0d);
        }
    }
}
