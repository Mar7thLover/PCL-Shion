using System.Threading;
using Avalonia.Controls;
using PCL.Shion.Controls;

namespace PCL.Shion.UI.Animation;

public static partial class ModAnimation
{
    public static void AniDispose(MyIconButton control, bool removeFromChildren,
        ParameterizedThreadStart? callBack = null)
    {
        if (!control.IsHitTestVisible)
            return;
        control.IsHitTestVisible = false;
        AniStart(new[]
        {
            AaScaleTransform(control, -1.5d, 200, ease: new AniEaseInFluent()),
            AaCode(() =>
            {
                if (removeFromChildren)
                    ((Panel)control.Parent!).Children.Remove(control);
                else
                    control.IsVisible = false;
                callBack?.Invoke(control);
            }, after: true)
        }, "MyIconButton Dispose " + control.Uuid);
    }
}
