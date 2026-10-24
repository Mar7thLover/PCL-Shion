using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.UI;

public static class HintService
{
    private struct HintMessage
    {
        public string Text;
        public HintType Type;
        public bool Log;
    }

    private static readonly object HintWaitingLock = new();
    private static readonly List<HintMessage> HintWaiting = [];
    private static DispatcherTimer? _tickTimer;

    public static void Hint(string? text, HintType type = HintType.Info, bool log = true)
    {
        var normalized = (text ?? "").Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
        lock (HintWaitingLock)
        {
            if (HintWaiting.Any(h => h.Text == normalized && h.Type == type)) return;
            HintWaiting.Add(new HintMessage { Text = normalized, Type = type, Log = log });
        }

        EnsureTicking();
    }

    internal static void Tick()
    {
        try
        {
            var frmMain = MainWindow.Current;
            if (frmMain?.PanHint is not { } panHint)
                return;

            panHint.HorizontalAlignment = HorizontalAlignment.Right;
            panHint.VerticalAlignment = VerticalAlignment.Bottom;

            var extraHeight = frmMain.PanExtraButtons.Bounds.Height;
            panHint.Margin = new Thickness(0, 0, 0, extraHeight > 0 ? extraHeight + 20 : 20);

            HintMessage currentHint;
            lock (HintWaitingLock)
            {
                if (HintWaiting.Count == 0)
                    return;
                currentHint = HintWaiting[0];
            }

            var duplicate = panHint.Children.OfType<MyToast>()
                .FirstOrDefault(t => !t.IsDismissing && t.Context == currentHint.Text && t.ToastType == currentHint.Type);
            if (duplicate != null)
            {
                duplicate.Emphasize();
                RemoveFirstHint();
                return;
            }

            var activeCount = panHint.Children.OfType<MyToast>().Count(t => !t.IsDismissing);
            if (activeCount >= 5)
            {
                var oldest = panHint.Children.OfType<MyToast>().FirstOrDefault(t => !t.IsDismissing);
                oldest?.Dismiss();
                return;
            }

            var toast = new MyToast
            {
                Context = currentHint.Text,
                ToastType = currentHint.Type,
                Icon = currentHint.Type switch
                {
                    HintType.Success => "lucide/circle-check",
                    HintType.Error => "lucide/circle-minus",
                    HintType.Warning => "lucide/triangle-alert",
                    _ => "lucide/info"
                },
                DisplayDuration = (800d + Math.Clamp(currentHint.Text.Length, 5, 23) * 180d) * ModAnimation.aniSpeed
            };

            panHint.Children.Add(toast);
            toast.Show();

            if (currentHint.Log)
                Trace.TraceInformation("[UI] 弹出提示：" + currentHint.Text);
            RemoveFirstHint();
        }
        catch (Exception ex)
        {
            Trace.TraceError("[UI] 显示弹出提示失败：" + ex);
        }
    }

    public static void HideAll()
    {
        if (MainWindow.Current?.PanHint is not { } panHint)
            return;
        foreach (var toast in panHint.Children.OfType<MyToast>().ToList())
            toast.Dismiss();
    }

    private static void RemoveFirstHint()
    {
        lock (HintWaitingLock)
        {
            if (HintWaiting.Count > 0)
                HintWaiting.RemoveAt(0);
        }
    }

    private static void EnsureTicking()
    {
        if (_tickTimer is not null)
            return;
        if (Dispatcher.UIThread.CheckAccess())
            StartTicking();
        else
            Dispatcher.UIThread.Post(StartTicking);
    }

    private static void StartTicking()
    {
        if (_tickTimer is not null)
            return;
        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _tickTimer.Tick += (_, _) => Tick();
        _tickTimer.Start();
    }
}
