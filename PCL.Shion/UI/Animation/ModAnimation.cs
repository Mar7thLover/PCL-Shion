using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace PCL.Shion.UI.Animation;

/// <summary>
/// 动画引擎，移植自 WPF 版 ModAnimation：帧时序、缓动计算以及各接口的名称、参数与语义均与 WPF 版一致。
/// </summary>
public static partial class ModAnimation
{
    private static int aniFPSCounter;
    private static long aniFPSTimer;

    /// <summary>
    /// 当前的动画 FPS。
    /// </summary>
    public static int aniFPS;

    /// <summary>
    /// 开始动画执行。
    /// </summary>
    /// <param name="fpsLimit">动画 FPS 上限，对应 WPF 版设置项 UiAniFPS（默认 59）。</param>
    public static void AniStart(int fpsLimit = 59)
    {
        // 初始化计时器
        aniLastTick = GetTimeTick();
        aniFPSTimer = aniLastTick;
        aniRunning = true; // 标记动画执行开始

        var minFrameGap = 1000d / (fpsLimit + 1) / 2;

        new Thread(() =>
        {
            try
            {
                while (true)
                {
                    // 两帧之间的间隔时间
                    var deltaTime = (long)Math.Round(MathClamp(GetTimeTick() - aniLastTick, 0, 100000));
                    if (deltaTime < minFrameGap)
                    {
                        // 限制 FPS
                        Thread.Sleep(1);
                        continue;
                    }

                    aniLastTick = GetTimeTick();
                    // 记录 FPS
                    if (MathClamp(aniLastTick - aniFPSTimer, 0d, 100000d) >= 500d)
                    {
                        aniFPS = aniFPSCounter;
                        aniFPSCounter = 0;
                        aniFPSTimer = aniLastTick;
                    }

                    aniFPSCounter += 2;

                    // 执行动画
                    RunInUiWait(() => AniTimer((int)Math.Round(deltaTime * aniSpeed)));
                }
            }
            catch (OperationCanceledException)
            {
                // UI 线程已关闭
            }
            catch (Exception ex)
            {
                Log(ex, "动画帧执行失败");
            }
        })
        {
            Name = "Animation",
            Priority = ThreadPriority.AboveNormal,
            // WPF 版在退出时直接结束进程，这里改为后台线程，随程序退出
            IsBackground = true
        }.Start();
    }

    /// <summary>
    /// 动画定时器事件。
    /// </summary>
    public static void AniTimer(int deltaTick)
    {
        try
        {
            var i = -1;
            // 循环每个动画组
            while (i + 1 < aniGroups.Count)
            {
                i += 1;
                // 初始化
                var entry = aniGroups.Values.ElementAt(i);
                if (entry.startTick > aniLastTick)
                    continue; // 跳过本刻之后开始的动画
                var canRemoveAfter = true; // 是否应该去除“之后”标记
                var ii = 0;

                // 循环每个动画
                while (ii < entry.data.Count)
                {
                    var anim = entry.data[ii];
                    // 执行种类
                    if (!anim.isAfter) // 之前
                    {
                        canRemoveAfter = false; // 取消“之后”标记
                        // 增加执行时间
                        anim.timeFinished += deltaTick;
                        // 执行动画
                        if (anim.timeFinished > 0)
                            anim = AniRun(anim);

                        // 如果当前动画已执行完毕
                        if (anim.timeFinished >= anim.timeTotal)
                        {
                            // 如果是去向颜色资源的动画，设置引用
                            if (anim.typeMain == AniType.Color)
                            {
                                var target = (object[])anim.obj!;
                                if (!string.Equals(target[2] as string, "", StringComparison.Ordinal))
                                    ((StyledElement)target[0]).SetResourceReference((AvaloniaProperty)target[1], target[2]!);
                            }

                            // 删除
                            entry.data.RemoveAt(ii);
                            continue;
                        }

                        entry.data[ii] = anim;
                    }
                    else if (canRemoveAfter) // 之后
                    {
                        // 之后改为之前
                        canRemoveAfter = false;
                        anim.isAfter = false;
                        entry.data[ii] = anim;
                        // 重新循环该动画
                        continue;
                    }
                    else
                    {
                        // 不能去除该“之后”标记，结束该动画组
                        break;
                    }

                    ii += 1;
                }

                // 如果当前动画组都执行完毕则删除
                if (entry.data.Count == 0)
                {
                    // 为了避免新添加的动画影响顺序，不能 RemoveAt(i)
                    // 为了允许动画在执行中添加同名动画组，不能按名字移除
                    for (int current = 0, loopTo = aniGroups.Count - 1; current <= loopTo; current++)
                    {
                        var group = aniGroups.ElementAt(current);
                        if (group.Value.Uuid == entry.Uuid)
                        {
                            aniGroups.TryRemove(group.Key, out _);
                            break;
                        }
                    }

                    i -= 1;
                }
            }
        }
        catch (Exception ex)
        {
            Log(ex, "动画刻执行失败");
        }
    }

    /// <summary>
    /// 执行一个动画。
    /// </summary>
    /// <param name="ani">执行的动画对象。</param>
    private static AniData AniRun(AniData ani)
    {
        try
        {
            switch (ani.typeMain)
            {
                case AniType.Number:
                {
                    var delta = MathPercent(0d, (double)ani.value!,
                        ani.ease!.GetDelta(ani.timeFinished / (double)ani.timeTotal, ani.timePercent));
                    if (delta != 0d)
                        switch (ani.typeSub)
                        {
                            case AniTypeSub.X:
                            {
                                DeltaLeft((Layoutable)ani.obj!, delta);
                                break;
                            }
                            case AniTypeSub.Y:
                            {
                                DeltaTop((Layoutable)ani.obj!, delta);
                                break;
                            }
                            case AniTypeSub.Opacity:
                            {
                                var obj = (dynamic)ani.obj!;
                                obj.Opacity = MathClamp(Convert.ToDouble((object)obj.Opacity) + delta, 0d, 1d);
                                break;
                            }
                            case AniTypeSub.Width:
                            {
                                var obj = (Layoutable)ani.obj!;
                                obj.Width = Math.Max((double.IsNaN(obj.Width) ? obj.Bounds.Width : obj.Width) + delta, 0d);
                                break;
                            }
                            case AniTypeSub.Height:
                            {
                                var obj = (Layoutable)ani.obj!;
                                obj.Height = Math.Max((double.IsNaN(obj.Height) ? obj.Bounds.Height : obj.Height) + delta, 0d);
                                break;
                            }
                            case AniTypeSub.Value:
                            {
                                ((dynamic)ani.obj!).Value += delta;
                                break;
                            }
                            case AniTypeSub.Radius:
                            {
                                ((dynamic)ani.obj!).Radius += delta;
                                break;
                            }
                            case AniTypeSub.StrokeThickness:
                            {
                                var obj = (dynamic)ani.obj!;
                                obj.StrokeThickness = Math.Max(Convert.ToDouble((object)obj.StrokeThickness) + delta, 0d);
                                break;
                            }
                            case AniTypeSub.BorderThickness:
                            {
                                var obj = (dynamic)ani.obj!;
                                obj.BorderThickness = new Thickness(((Thickness)obj.BorderThickness).Bottom + delta);
                                break;
                            }
                            case AniTypeSub.TranslateX:
                            {
                                var obj = (Visual)ani.obj!;
                                if (obj.RenderTransform is not TranslateTransform transform)
                                    obj.RenderTransform = transform = new TranslateTransform(0d, 0d);
                                transform.X += delta;
                                break;
                            }
                            case AniTypeSub.TranslateY:
                            {
                                var obj = (Visual)ani.obj!;
                                if (obj.RenderTransform is not TranslateTransform transform)
                                    obj.RenderTransform = transform = new TranslateTransform(0d, 0d);
                                transform.Y += delta;
                                break;
                            }
                            case AniTypeSub.Double:
                            {
                                var target = (object[])ani.obj!;
                                var obj = (AvaloniaObject)target[0];
                                var prop = (AvaloniaProperty)target[1];
                                obj.SetValue(prop, Convert.ToDouble(obj.GetValue(prop)) + delta);
                                break;
                            }
                            case AniTypeSub.DoubleParam:
                            {
                                ((ParameterizedThreadStart)ani.obj!)(delta);
                                break;
                            }
                            case AniTypeSub.GridLengthWidth:
                            {
                                var obj = (dynamic)ani.obj!;
                                obj.Width = new GridLength(
                                    Math.Max(Convert.ToDouble((object)obj.Width.Value) + delta, 0d), GridUnitType.Star);
                                break;
                            }
                        }

                    break;
                }

                case AniType.Color:
                {
                    // 利用 valueLast 记录了余下的小数值
                    var delta = MathPercent(new MyColor(0d, 0d, 0d, 0d), (MyColor)ani.value!,
                                    ani.ease!.GetDelta(ani.timeFinished / (double)ani.timeTotal, ani.timePercent)) +
                                (MyColor)ani.valueLast!;
                    var target = (object[])ani.obj!;
                    var obj = (StyledElement)target[0];
                    var prop = (AvaloniaProperty)target[1];
                    var newColor = new MyColor(obj.GetValue(prop)) + delta;
                    ResourceReferenceExtensions.SetValueReplacingBinding(obj, prop,
                        prop.PropertyType == typeof(Color) ? (Color)newColor : (SolidColorBrush)newColor);
                    ani.valueLast = newColor - new MyColor(obj.GetValue(prop));
                    break;
                }

                case AniType.Scale:
                {
                    var obj = (Layoutable)ani.obj!;
                    var rect = (ScaleRect)ani.value!;
                    var delta = ani.ease!.GetDelta(ani.timeFinished / (double)ani.timeTotal, ani.timePercent);
                    obj.Margin = new Thickness(
                        obj.Margin.Left + MathPercent(0d, rect.Left, delta),
                        obj.Margin.Top + MathPercent(0d, rect.Top, delta),
                        obj.Margin.Right + MathPercent(0d, rect.Left, delta),
                        obj.Margin.Bottom + MathPercent(0d, rect.Top, delta));
                    obj.Width = Math.Max(obj.Width + MathPercent(0d, rect.Width, delta), 0d);
                    obj.Height = Math.Max(obj.Height + MathPercent(0d, rect.Height, delta), 0d);
                    break;
                }

                case AniType.TextAppear:
                {
                    var value = (object[])ani.value!;
                    var hideFlag = (bool)value[1];
                    var originalText = value[0].ToString()!;
                    var textLength = originalText.Length;
                    var textCount = (int)Math.Round(
                        (double)(hideFlag ? textLength : 0) + Math.Round(
                            textLength *
                            (hideFlag ? -1 : 1) *
                            ani.ease!.GetDelta(ani.timeFinished / (double)ani.timeTotal, 0d)));
                    var newText = originalText.Substring(0, Math.Min(textCount, originalText.Length));
                    // 添加乱码
                    if (textCount < originalText.Length)
                    {
                        if (originalText[textCount] >= 128)
                            newText += Gb18030.Instance.GetString(new[]
                            {
                                (byte)NextInt(16 + 160, 87 + 160),
                                (byte)NextInt(1 + 160, 89 + 160)
                            });
                        else
                            newText += TextAppearChars[Random.Shared.Next(TextAppearChars.Length)];
                    }

                    // 设置文本
                    if (ani.obj is TextBlock textBlock)
                        textBlock.Text = newText;
                    else
                        ((dynamic)ani.obj!).Context = newText;

                    break;
                }

                case AniType.Code:
                {
                    ((ThreadStart)ani.value!)();
                    break;
                }

                case AniType.ScaleTransform:
                {
                    var obj = (Visual)ani.obj!;
                    if (obj.RenderTransform is not ScaleTransform transform)
                    {
                        obj.RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
                        obj.RenderTransform = transform = new ScaleTransform(1d, 1d);
                    }

                    var delta = MathPercent(0d, (double)ani.value!,
                        ani.ease!.GetDelta(ani.timeFinished / (double)ani.timeTotal, ani.timePercent));
                    transform.ScaleX = Math.Max(transform.ScaleX + delta, 0d);
                    transform.ScaleY = Math.Max(transform.ScaleY + delta, 0d);
                    break;
                }

                case AniType.RotateTransform:
                {
                    var obj = (Visual)ani.obj!;
                    if (obj.RenderTransform is not RotateTransform transform)
                    {
                        obj.RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
                        obj.RenderTransform = transform = new RotateTransform(0d);
                    }

                    var delta = MathPercent(0d, (double)ani.value!,
                        ani.ease!.GetDelta(ani.timeFinished / (double)ani.timeTotal, ani.timePercent));
                    transform.Angle = transform.Angle + delta;
                    break;
                }
            }

            ani.timePercent = ani.timeFinished / (double)ani.timeTotal; // 修改执行百分比
        }
        catch (Exception ex)
        {
            Log(ex, "执行动画失败：" + ani);
        }

        return ani;
    }

    #region 声明

    /// <summary>
    /// 动画速度。最大为 200。
    /// </summary>
    public static double aniSpeed = 1d;

    /// <summary>
    /// 动画组列表。
    /// </summary>
    public static ConcurrentDictionary<string, AniGroupEntry> aniGroups = new();

    public class AniGroupEntry
    {
        public List<AniData> data = [];
        public long startTick;
        public int Uuid = GetUuid();
    }

    /// <summary>
    /// 上一次记刻的时间。
    /// </summary>
    private static long aniLastTick;

    /// <summary>
    /// 动画模块是否正在运行。
    /// </summary>
    public static bool aniRunning;

    private static readonly object aniControlEnabledLock = new();

    /// <summary>
    /// 控件动画执行是否开启。先 +1，再 -1。
    /// </summary>
    public static int AniControlEnabled
    {
        get => field;
        set
        {
            lock (aniControlEnabledLock)
            {
                field = value;
            }
        }
    }

    #endregion

    #region 接口（开始、中断、检测）

    /// <summary>
    /// 开始一个动画组。
    /// </summary>
    /// <param name="aniGroup">由 Aa 开头的函数初始化的 AniData 对象集合。</param>
    /// <param name="name">动画组的名称。如果重复会直接停止同名动画组。</param>
    /// <param name="refreshTime">是否重置记刻时间，避免处理动画时已经造成的延迟让动画突然结束。</param>
    public static void AniStart(IList aniGroup, string name = "", bool refreshTime = false)
    {
        if (refreshTime)
            aniLastTick = GetTimeTick(); // 避免处理动画时已经造成了极大的延迟，导致动画突然结束
        // 添加到正在执行的动画组
        var newEntry = new AniGroupEntry { data = GetFullList<AniData>(aniGroup), startTick = GetTimeTick() };
        if (string.IsNullOrEmpty(name))
            name = newEntry.Uuid.ToString(CultureInfo.InvariantCulture);
        else
            AniStop(name);
        aniGroups.TryAdd(name, newEntry);
    }

    /// <summary>
    /// 开始一个动画组。
    /// </summary>
    public static void AniStart(AniData aniGroup, string name = "", bool refreshTime = false)
    {
        AniStart(new List<AniData> { aniGroup }, name, refreshTime);
    }

    /// <summary>
    /// 直接停止一个动画组。
    /// </summary>
    /// <param name="name">需要停止的动画组的名称。</param>
    public static void AniStop(string name)
    {
        aniGroups.TryRemove(name, out _);
    }

    /// <summary>
    /// 获取动画是否正在进行中。
    /// </summary>
    public static bool AniIsRun(string name)
    {
        return aniGroups.ContainsKey(name);
    }

    #endregion

    #region 辅助（数学与边距方法与 WPF 版 ModBase 中的同名方法一致）

    private const string TextAppearChars =
        @"0123456789./*-+\[]{};':/?,!@#$%^&*()_+-=qwwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM";

    private static int uuid = 1;

    /// <summary>
    /// TextAppear 生成中文乱码所用的编码，首次使用时注册代码页编码。
    /// </summary>
    private static class Gb18030
    {
        public static readonly Encoding Instance = Create();

        private static Encoding Create()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding("GB18030");
        }
    }

    /// <summary>
    /// 窗口位置是整数像素，记录每个窗口尚未移动的小数部分，避免缓慢移动被舍入吞掉。
    /// </summary>
    private static readonly ConditionalWeakTable<Window, StrongBox<Vector>> windowPositionRemainders = new();

    private static long GetTimeTick() => Environment.TickCount64;

    private static int GetUuid() => Interlocked.Increment(ref uuid);

    private static int NextInt(int min, int max) => Random.Shared.Next(min, max + 1);

    private static double MathClamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

    /// <summary>
    /// 获取两数间的百分比。小数点精确到 6 位。
    /// </summary>
    private static double MathPercent(double valueA, double valueB, double percent) =>
        Math.Round(valueA * (1d - percent) + valueB * percent, 6); // 解决 Double 计算错误

    /// <summary>
    /// 获取两颜色间的百分比，根据 RGB 计算。小数点精确到 6 位。
    /// </summary>
    private static MyColor MathPercent(MyColor valueA, MyColor valueB, double percent)
    {
        var col = valueA * (1d - percent) + valueB * percent;
        return new MyColor
        {
            a = Math.Round(col.a, 6), r = Math.Round(col.r, 6), g = Math.Round(col.g, 6), b = Math.Round(col.b, 6)
        };
    }

    /// <summary>
    /// 将元素与 List 的混合体拆分为元素组。
    /// </summary>
    private static List<T> GetFullList<T>(IList data)
    {
        var result = new List<T>();
        for (var i = 0; i < data.Count; i++)
            if (data[i] is ICollection)
                result.AddRange((IEnumerable<T>)data[i]!);
            else
                result.Add((T)data[i]!);
        return result;
    }

    /// <summary>
    /// 在 UI 线程同步执行。
    /// </summary>
    private static void RunInUiWait(Action action)
    {
        if (Application.Current is null)
            return;
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Invoke(action);
    }

    /// <summary>
    /// 相对增减控件的左边距。
    /// </summary>
    private static void DeltaLeft(Layoutable control, double newValue)
    {
        CheckDelta(newValue);

        if (control is Window window)
        {
            // 窗口改变
            DeltaWindowPosition(window, newValue, 0d);
            return;
        }

        // 根据 HorizontalAlignment 改变数值
        switch (control.HorizontalAlignment)
        {
            case HorizontalAlignment.Left:
            case HorizontalAlignment.Stretch:
            {
                control.Margin = new Thickness(control.Margin.Left + newValue, control.Margin.Top,
                    control.Margin.Right, control.Margin.Bottom);
                break;
            }
            case HorizontalAlignment.Right:
            {
                control.Margin = new Thickness(control.Margin.Left, control.Margin.Top,
                    control.Margin.Right - newValue, control.Margin.Bottom);
                break;
            }
            default:
                throw new InvalidOperationException("横向移动不支持对齐方式 " + control.HorizontalAlignment);
        }
    }

    /// <summary>
    /// 相对增减控件的上边距。
    /// </summary>
    private static void DeltaTop(Layoutable control, double newValue)
    {
        CheckDelta(newValue);

        if (control is Window window)
        {
            // 窗口改变
            DeltaWindowPosition(window, 0d, newValue);
            return;
        }

        // 根据 VerticalAlignment 改变数值
        switch (control.VerticalAlignment)
        {
            case VerticalAlignment.Top:
            {
                control.Margin = new Thickness(control.Margin.Left, control.Margin.Top + newValue,
                    control.Margin.Right, control.Margin.Bottom);
                break;
            }
            case VerticalAlignment.Bottom:
            {
                control.Margin = new Thickness(control.Margin.Left, control.Margin.Top, control.Margin.Right,
                    control.Margin.Bottom - newValue);
                break;
            }
            default:
                throw new InvalidOperationException("纵向移动不支持对齐方式 " + control.VerticalAlignment);
        }
    }

    private static void CheckDelta(double newValue)
    {
        if (double.IsNaN(newValue) || double.IsInfinity(newValue))
            throw new ArgumentOutOfRangeException(nameof(newValue), newValue, "位移量必须是有限数值");
    }

    /// <summary>
    /// 按与设备无关的单位移动窗口，与 WPF 的 Window.Left / Top 相同。
    /// </summary>
    private static void DeltaWindowPosition(Window window, double deltaX, double deltaY)
    {
        var remainder = windowPositionRemainders.GetOrCreateValue(window);
        var scaling = window.DesktopScaling;
        var x = remainder.Value.X + deltaX * scaling;
        var y = remainder.Value.Y + deltaY * scaling;
        var moveX = (int)Math.Truncate(x);
        var moveY = (int)Math.Truncate(y);
        remainder.Value = new Vector(x - moveX, y - moveY);
        if (moveX != 0 || moveY != 0)
            window.Position = new PixelPoint(window.Position.X + moveX, window.Position.Y + moveY);
    }

    private static void Log(Exception ex, string message)
    {
        Trace.TraceError("[Animation] " + message + "：" + ex);
    }

    #endregion
}
