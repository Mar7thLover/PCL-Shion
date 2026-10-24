using Avalonia;
using Avalonia.Controls;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

internal static class ControlVisualHelpers
{
    internal static bool ShouldAnimate(Control control, object? animationOverride = null)
    {
        return control.IsLoaded && ModAnimation.AniControlEnabled == 0 && !false.Equals(animationOverride);
    }

    internal static void AnimateColorOrSetResource(Control target, AvaloniaProperty property,
        string resourceKey, int duration, string animationKey, bool shouldAnimate)
    {
        if (shouldAnimate)
        {
            ModAnimation.AniStart(ModAnimation.AaColor(target, property, resourceKey, duration), animationKey);
        }
        else
        {
            ModAnimation.AniStop(animationKey);
            target.SetResourceReference(property, resourceKey);
        }
    }
}
