using System.Threading;
using Avalonia.Controls;
using PCL.Shion.Controls;

namespace PCL.Shion.UI.Animation;

public static partial class ModAnimation
{
    public static void AniDispose(MyCard control, bool removeFromChildren, ParameterizedThreadStart? callBack = null)
    {
        if (control.IsHitTestVisible)
        {
            control.IsHitTestVisible = false;
            AniStart(new[]
            {
                AaScaleTransform(control, -0.08d, 200, ease: new AniEaseInFluent()),
                AaOpacity(control, -1, 200, ease: new AniEaseOutFluent()),
                AaHeight(control, -control.Bounds.Height, 150, 100, new AniEaseOutFluent()),
                AaCode(() =>
                {
                    if (removeFromChildren)
                    {
                        if (control.Parent is null)
                            return;
                        ((Panel)control.Parent).Children.Remove(control);
                    }
                    else
                    {
                        control.IsVisible = false;
                    }

                    callBack?.Invoke(control);
                }, after: true)
            }, "MyCard Dispose " + control.uuid);
        }
        else
        {
            if (removeFromChildren)
            {
                if (control.Parent is null)
                    return;
                ((Panel)control.Parent).Children.Remove(control);
            }
            else
            {
                control.IsVisible = false;
            }

            callBack?.Invoke(control);
        }
    }
}
