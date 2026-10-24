using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Controls;

namespace PCL.Shion.UI.Animation;

public static partial class ModAnimation
{
    #region 类与枚举

    /// <summary>
    /// 单个动画对象。
    /// </summary>
    public struct AniData
    {
        /// <summary>
        /// 动画种类。
        /// </summary>
        public AniType typeMain;

        /// <summary>
        /// 动画副种类。
        /// </summary>
        public AniTypeSub typeSub;

        /// <summary>
        /// 动画总长度。
        /// </summary>
        public int timeTotal;

        /// <summary>
        /// 已经执行的动画长度。如果为负数则为延迟。
        /// </summary>
        public int timeFinished;

        /// <summary>
        /// 已经完成的百分比。
        /// </summary>
        public double timePercent;

        /// <summary>
        /// 是否为“以后”。
        /// </summary>
        public bool isAfter;

        /// <summary>
        /// 插值器类型。
        /// </summary>
        public AniEase? ease;

        /// <summary>
        /// 动画对象。
        /// </summary>
        public object? obj;

        /// <summary>
        /// 动画值。
        /// </summary>
        public object? value;

        /// <summary>
        /// 上次执行时的动画值。
        /// </summary>
        public object? valueLast;

        public override string ToString()
        {
            return typeMain + " | " + timeFinished + "/" + timeTotal + "(" +
                   Math.Round(timePercent * 100d) + "%)" +
                   (obj is null ? "" : " | " + obj + "(" + obj.GetType().Name + ")");
        }
    }

    /// <summary>
    /// 动画基础种类。
    /// </summary>
    public enum AniType
    {
        /// <summary>
        /// 单个 Double 的动画，包括位置、长宽、透明度等。这需要附属类型。
        /// </summary>
        Number,

        /// <summary>
        /// 颜色属性的动画。
        /// </summary>
        Color,

        /// <summary>
        /// 缩放控件大小。比起 4 个 Double 动画来说效率更高。
        /// </summary>
        Scale,

        /// <summary>
        /// 文字一个个出现。
        /// </summary>
        TextAppear,

        /// <summary>
        /// 执行代码。
        /// </summary>
        Code,

        /// <summary>
        /// 以 RenderTransform 方式缩放控件。
        /// </summary>
        ScaleTransform,

        /// <summary>
        /// 以 RenderTransform 方式旋转控件。
        /// </summary>
        RotateTransform
    }

    /// <summary>
    /// 动画扩展种类。
    /// </summary>
    public enum AniTypeSub
    {
        X,
        Y,
        Width,
        Height,
        Opacity,
        Value,
        Radius,
        BorderThickness,
        StrokeThickness,
        TranslateX,
        TranslateY,
        Double,
        DoubleParam,
        GridLengthWidth
    }

    /// <summary>
    /// AaScale 的变化量：Left / Top 为边距的变化，Width / Height 为尺寸的变化。
    /// </summary>
    private sealed record ScaleRect(double Left, double Top, double Width, double Height);

    #endregion

    #region 种类

    // Double 动画

    /// <summary>
    /// 移动 X 轴的动画（改变 Margin，窗口则改变位置）。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">进行移动的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaX(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.X,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    /// 移动 Y 轴的动画（改变 Margin，窗口则改变位置）。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">进行移动的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaY(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Y,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    /// 改变宽度的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">宽度改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaWidth(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Width,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    /// 改变高度的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">高度改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaHeight(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Height,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    /// 改变透明度的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">透明度改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaOpacity(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Opacity,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    /// 改变对象的 Value 属性的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">Value 属性改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaValue(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Value,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    /// 改变对象的 Radius 属性的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">Radius 属性改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaRadius(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.Radius,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    /// 改变对象的 BorderThickness 属性的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">BorderThickness 属性改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaBorderThickness(object obj, double value, int time = 400, int delay = 0,
        AniEase? ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.BorderThickness,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    /// 改变对象的 StrokeThickness 属性的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">StrokeThickness 属性改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaStrokeThickness(object obj, double value, int time = 400, int delay = 0,
        AniEase? ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.StrokeThickness,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    /// 改变 Width 的 GridLength 属性的动画。必须为 Star。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">GridLength.Value 改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaGridLengthWidth(object obj, double value, int time = 400, int delay = 0,
        AniEase? ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.GridLengthWidth,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    // Double 动画（Obj, Prop）

    /// <summary>
    /// 改变数字属性的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="prop">动画的属性。</param>
    /// <param name="value">改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaDouble(object obj, AvaloniaProperty prop, double value, int time = 400, int delay = 0,
        AniEase? ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number, typeSub = AniTypeSub.Double, timeTotal = time,
            ease = ease ?? new AniEaseLinear(), obj = new[] { obj, prop, "" }, value = value, isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    /// 获取数字动画值。每帧以本帧的增量（double）调用 lambda。
    /// </summary>
    /// <param name="lambda">接收增量的回调。</param>
    /// <param name="value">改变的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaDouble(ParameterizedThreadStart lambda, double value, int time = 400, int delay = 0,
        AniEase? ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number, typeSub = AniTypeSub.DoubleParam, timeTotal = time,
            ease = ease ?? new AniEaseLinear(), obj = lambda, value = value, isAfter = after, timeFinished = -delay
        };
    }

    // 颜色动画（Obj, Prop, [Res]）

    /// <summary>
    /// 改变颜色属性的动画。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="prop">动画的属性，类型为 Color 或画刷。</param>
    /// <param name="value">颜色改变的值。以 RGB 加减法进行计算。不用担心超额。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaColor(StyledElement obj, AvaloniaProperty prop, MyColor value, int time = 400,
        int delay = 0, AniEase? ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Color, timeTotal = time, ease = ease ?? new AniEaseLinear(),
            obj = new object[] { obj, prop, "" }, value = value, isAfter = after, timeFinished = -delay,
            valueLast = new MyColor(0d, 0d, 0d, 0d)
        };
    }

    /// <summary>
    /// 改变颜色属性为一个资源的动画。动画结束后属性会引用该资源。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="prop">动画的属性，类型为 Color 或画刷。</param>
    /// <param name="res">要将颜色改变为该资源值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaColor(StyledElement obj, AvaloniaProperty prop, string res, int time = 400,
        int delay = 0, AniEase? ease = null, bool after = false)
    {
        if (!Application.Current!.TryFindResource(res, out var resource))
            throw new KeyNotFoundException("找不到资源 " + res);
        return new AniData
        {
            typeMain = AniType.Color, timeTotal = time, ease = ease ?? new AniEaseLinear(),
            obj = new object[] { obj, prop, res },
            value = new MyColor(resource) - new MyColor(obj.GetValue(prop)),
            isAfter = after, timeFinished = -delay, valueLast = new MyColor(0d, 0d, 0d, 0d)
        };
    }

    // Scale

    /// <summary>
    /// 缩放控件的动画（同时改变 Margin 与 Width / Height）。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">大小改变的百分比（如 -0.6）或值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    /// <param name="absolute">大小改变是否为绝对值。若为 True 则为绝对像素，若为 False 则为相对百分比。</param>
    public static AniData AaScale(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null,
        bool after = false, bool absolute = false)
    {
        ScaleRect changeRect;
        if (absolute)
        {
            changeRect = new ScaleRect(-0.5d * value, -0.5d * value, value, value);
        }
        else
        {
            var size = ((Visual)obj).Bounds;
            changeRect = new ScaleRect(
                -0.5d * size.Width * value,
                -0.5d * size.Height * value,
                size.Width * value,
                size.Height * value);
        }

        return new AniData
        {
            typeMain = AniType.Scale, timeTotal = time, ease = ease ?? new AniEaseLinear(), obj = obj,
            value = changeRect, isAfter = after, timeFinished = -delay
        };
    }

    // TextAppear

    /// <summary>
    /// 让一段文字一个个字出现或消失的动画。
    /// </summary>
    /// <param name="obj">动画的对象。必须是 TextBlock，或拥有 Context 属性的控件。</param>
    /// <param name="hide">是否为一个个字隐藏。默认为 False（一个个字出现）。这些字必须已经存在了。</param>
    /// <param name="timePerText">是否采用根据文本长度决定时间的方式。</param>
    /// <param name="time">动画长度（毫秒）。若 timePerText 为 True，这代表每个字所占据的时间。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaTextAppear(object obj, bool hide = false, bool timePerText = true, int time = 70,
        int delay = 0, AniEase? ease = null, bool after = false)
    {
        // Are we cool yet？
        string text = obj is TextBlock textBlock ? textBlock.Text ?? "" : ((dynamic)obj).Context.ToString();
        return new AniData
        {
            typeMain = AniType.TextAppear, ease = ease ?? new AniEaseLinear(),
            timeTotal = timePerText ? time * text.Length : time,
            obj = obj,
            value = new object[] { text, hide },
            isAfter = after, timeFinished = -delay
        };
    }

    // Code

    /// <summary>
    /// 执行代码。
    /// </summary>
    /// <param name="code">一个 ThreadStart。这将会在执行时在 UI 线程调用。</param>
    /// <param name="delay">代码延迟执行的时间（毫秒）。</param>
    /// <param name="after">是否等到以前的动画完成后才执行。</param>
    public static AniData AaCode(ThreadStart code, int delay = 0, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Code,
            timeTotal = 1,
            value = code,
            isAfter = after,
            timeFinished = -delay
        };
    }

    // ScaleTransform

    /// <summary>
    /// 以 ScaleTransform 缩放控件的动画。若控件的 RenderTransform 不是 ScaleTransform，会以中心为原点替换为新的 ScaleTransform。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">大小改变的百分比（如 -0.6）。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaScaleTransform(object obj, double value, int time = 400, int delay = 0,
        AniEase? ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.ScaleTransform, timeTotal = time, ease = ease ?? new AniEaseLinear(), obj = obj,
            value = value, isAfter = after, timeFinished = -delay
        };
    }

    // RotateTransform

    /// <summary>
    /// 以 RotateTransform 旋转控件的动画。若控件的 RenderTransform 不是 RotateTransform，会以中心为原点替换为新的 RotateTransform。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">旋转改变的角度。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaRotateTransform(object obj, double value, int time = 400, int delay = 0,
        AniEase? ease = null, bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.RotateTransform, timeTotal = time, ease = ease ?? new AniEaseLinear(), obj = obj,
            value = value, isAfter = after, timeFinished = -delay
        };
    }

    // TranslateTransform

    /// <summary>
    /// 利用 TranslateTransform 移动 X 轴的动画，这不会造成布局更新。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">进行移动的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaTranslateX(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.TranslateX,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    /// <summary>
    /// 利用 TranslateTransform 移动 Y 轴的动画，这不会造成布局更新。
    /// </summary>
    /// <param name="obj">动画的对象。</param>
    /// <param name="value">进行移动的值。</param>
    /// <param name="time">动画长度（毫秒）。</param>
    /// <param name="delay">动画延迟执行的时间（毫秒）。</param>
    /// <param name="ease">插值器类型。</param>
    /// <param name="after">是否等到以前的动画完成后才继续本动画。</param>
    public static AniData AaTranslateY(object obj, double value, int time = 400, int delay = 0, AniEase? ease = null,
        bool after = false)
    {
        return new AniData
        {
            typeMain = AniType.Number,
            typeSub = AniTypeSub.TranslateY,
            timeTotal = time,
            ease = ease ?? new AniEaseLinear(),
            obj = obj,
            value = value,
            isAfter = after,
            timeFinished = -delay
        };
    }

    // 特殊

    /// <summary>
    /// 将一个 StackPanel 中的各个项目依次显示。
    /// </summary>
    public static List<AniData> AaStack(StackPanel stack, int time = 100, int delay = 25)
    {
        var result = new List<AniData>();
        var aniDelay = 0;
        foreach (var item in stack.Children)
        {
            item.Opacity = 0;
            result.Add(AaOpacity(item, 1d, time, aniDelay));
            aniDelay += delay;
        }

        return result;
    }

    #endregion
}
