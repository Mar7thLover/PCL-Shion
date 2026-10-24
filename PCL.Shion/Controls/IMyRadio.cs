using System;

namespace PCL.Shion.Controls;

/// <summary>
///     用于储存 RaiseByMouse 的 EventArgs。
/// </summary>
public sealed class RouteEventArgs : EventArgs
{
    public bool handled = false;
    public bool raiseByMouse;

    public RouteEventArgs(bool raiseByMouse = false)
    {
        this.raiseByMouse = raiseByMouse;
    }
}

public interface IMyRadio
{
    delegate void ChangedEventHandler(object sender, RouteEventArgs e);

    delegate void CheckEventHandler(object sender, RouteEventArgs e);

    event CheckEventHandler? Check;
    event ChangedEventHandler? Changed;
}
