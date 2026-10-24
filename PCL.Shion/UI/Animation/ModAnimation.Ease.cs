using System;

namespace PCL.Shion.UI.Animation;

public static partial class ModAnimation
{
    #region 缓动函数

    // 基类
    public enum AniEasePower
    {
        Weak = 2,
        Middle = 3,
        Strong = 4,
        ExtraStrong = 5
    }

    /// <summary>
    /// 缓动函数基类。
    /// </summary>
    public abstract class AniEase
    {
        /// <summary>
        /// 获取函数值。
        /// </summary>
        /// <param name="t">时间百分比。</param>
        public abstract double GetValue(double t);

        /// <summary>
        /// 获取增量值。
        /// </summary>
        /// <param name="t1">较大的 X。</param>
        /// <param name="t0">较小的 X。</param>
        public virtual double GetDelta(double t1, double t0)
        {
            return GetValue(t1) - GetValue(t0);
        }
    }

    /// <summary>
    /// 渐入渐出组合。
    /// </summary>
    public class AniEaseInout : AniEase
    {
        private readonly AniEase easeIn;
        private readonly double easeInPercent;
        private readonly AniEase easeOut;

        public AniEaseInout(AniEase easeIn, AniEase easeOut, double easeInPercent = 0.5d)
        {
            this.easeIn = easeIn;
            this.easeOut = easeOut;
            this.easeInPercent = easeInPercent;
        }

        public override double GetValue(double t)
        {
            if (t < easeInPercent) return easeInPercent * easeIn.GetValue(t / easeInPercent);

            return (1d - easeInPercent) * easeOut.GetValue((t - easeInPercent) / (1d - easeInPercent)) + easeInPercent;
        }
    }

    // Linear / 线性
    /// <summary>
    /// 线性，无缓动。
    /// </summary>
    public class AniEaseLinear : AniEase
    {
        public override double GetValue(double t)
        {
            return MathClamp(t, 0d, 1d);
        }

        public override double GetDelta(double t1, double t0)
        {
            return MathClamp(t1, 0d, 1d) - MathClamp(t0, 0d, 1d);
        }
    }

    // Fluent / 平滑
    /// <summary>
    /// 平滑开始。
    /// </summary>
    public class AniEaseInFluent : AniEase
    {
        private readonly AniEasePower p;

        public AniEaseInFluent(AniEasePower power = AniEasePower.Middle)
        {
            p = power;
        }

        public override double GetValue(double t)
        {
            return Math.Pow(MathClamp(t, 0d, 1d), (double)p);
        }
    }

    /// <summary>
    /// 平滑结束。
    /// </summary>
    public class AniEaseOutFluent : AniEase
    {
        private readonly AniEasePower p;

        public AniEaseOutFluent(AniEasePower power = AniEasePower.Middle)
        {
            p = power;
        }

        public override double GetValue(double t)
        {
            return 1d - Math.Pow(MathClamp(1d - t, 0d, 1d), (double)p);
        }
    }

    /// <summary>
    /// 平滑开始与结束。
    /// </summary>
    public class AniEaseInoutFluent : AniEase
    {
        private readonly AniEaseInout ease;

        public AniEaseInoutFluent(AniEasePower power = AniEasePower.Middle, double middle = 0.5d)
        {
            ease = new AniEaseInout(new AniEaseInFluent(power), new AniEaseOutFluent(power), middle);
        }

        public override double GetValue(double t)
        {
            return ease.GetValue(t);
        }
    }

    /// <summary>
    /// 以特定速度开始的平滑结束。
    /// </summary>
    public class AniEaseOutFluentWithInitial : AniEase
    {
        private readonly double alpha; // (初速度 / 平均速度) – 1

        /// <param name="initialPixelPerSecond">初速度，px/s</param>
        /// <param name="totalSecond">总时长，s</param>
        /// <param name="totalDistance">总路程，px</param>
        public AniEaseOutFluentWithInitial(double initialPixelPerSecond, double totalSecond, double totalDistance)
        {
            var v0Norm = initialPixelPerSecond * totalSecond / totalDistance; // 归一化初速度
            alpha = v0Norm - 1.0d;
            if (alpha < 0d)
                alpha = 0d; // 初速度小于平均速度时，退化为线性
        }

        public override double GetValue(double percent)
        {
            var p = MathClamp(percent, 0d, 1d);
            if (alpha == 0d)
                return p; // 退化到线性
            return (alpha + 1d) * p / (1d + alpha * p);
        }
    }

    // Back / 回弹
    /// <summary>
    /// 回弹开始。有效时间为 1/3。
    /// </summary>
    public class AniEaseInBack : AniEase
    {
        private readonly double p;

        public AniEaseInBack(AniEasePower power = AniEasePower.Middle)
        {
            p = 3d - (double)power * 0.5d;
        }

        public override double GetValue(double t)
        {
            t = MathClamp(t, 0d, 1d);
            return Math.Pow(t, p) * Math.Cos(1.5d * Math.PI * (1d - t));
        }
    }

    /// <summary>
    /// 回弹结束。有效时间为 1/3。
    /// </summary>
    public class AniEaseOutBack : AniEase
    {
        private readonly double p;

        public AniEaseOutBack(AniEasePower power = AniEasePower.Middle)
        {
            p = 3d - (double)power * 0.5d;
        }

        public override double GetValue(double t)
        {
            t = MathClamp(t, 0d, 1d);
            return 1d - Math.Pow(1d - t, p) * Math.Cos(1.5d * Math.PI * t);
        }
    }

    // Car / 平滑-回弹
    /// <summary>
    /// 回弹开始，短平滑结束。
    /// </summary>
    public class AniEaseInCar : AniEase
    {
        private readonly AniEaseInout ease;

        public AniEaseInCar(double middle = 0.7d, AniEasePower power = AniEasePower.Middle)
        {
            ease = new AniEaseInout(new AniEaseInBack(power), new AniEaseOutFluent(power), middle);
        }

        public override double GetValue(double t)
        {
            return ease.GetValue(t);
        }
    }

    /// <summary>
    /// 短平滑开始，回弹结束。
    /// </summary>
    public class AniEaseOutCar : AniEase
    {
        private readonly AniEaseInout ease;

        public AniEaseOutCar(double middle = 0.3d, AniEasePower power = AniEasePower.Middle)
        {
            ease = new AniEaseInout(new AniEaseInFluent(power), new AniEaseOutBack(power), middle);
        }

        public override double GetValue(double t)
        {
            return ease.GetValue(t);
        }
    }

    // Elastic / 弹簧
    /// <summary>
    /// 弹簧开始。约在 60% 到达最小值。
    /// </summary>
    public class AniEaseInElastic : AniEase
    {
        private readonly int p; // 6~9

        public AniEaseInElastic(AniEasePower power = AniEasePower.Middle)
        {
            p = (int)power + 4;
        }

        public override double GetValue(double t)
        {
            t = MathClamp(t, 0d, 1d);
            return Math.Pow(t, (p - 1) * 0.25d) * Math.Cos((p - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d));
        }
    }

    /// <summary>
    /// 弹簧结束。约在 40% 到达最大值。
    /// </summary>
    public class AniEaseOutElastic : AniEase
    {
        private readonly int p;

        public AniEaseOutElastic(AniEasePower power = AniEasePower.Middle)
        {
            p = (int)power + 4;
        }

        public override double GetValue(double t)
        {
            t = 1d - MathClamp(t, 0d, 1d);
            return 1d - Math.Pow(t, (p - 1) * 0.25d) * Math.Cos((p - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d));
        }
    }

    #endregion
}
