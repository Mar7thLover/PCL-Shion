using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PCL.Core.Logging;

namespace PCL;

/// <summary>
/// 启动时显示的开屏图标。
/// <para>
/// WPF 自带的 <see cref="System.Windows.SplashScreen"/> 会按照图片的像素尺寸开窗：它既不会随系统缩放（DPI）放大，
/// 读取 .ico 时也只会使用其中的第一帧。因此在高分屏上开屏图标会显得非常小。
/// 这里改用自绘窗口，按设备无关单位（DIP）显示图标，使其在任何缩放比例下都保持相同大小。
/// </para>
/// </summary>
public sealed class StartupSplash
{
    /// <summary>
    /// 图标的显示尺寸（设备无关单位）。
    /// </summary>
    private const double IconSize = 128d;

    /// <summary>
    /// 图标资源。这里使用 PNG 而不是 .ico，因为 .ico 中含有多帧，按需解码即可得到清晰的大图。
    /// </summary>
    private const string IconUri = "pack://application:,,,/Images/icon.png";

    private SplashWindow? _window;
    private System.Windows.SplashScreen? _fallbackSplash;

    private StartupSplash()
    {
    }

    /// <summary>
    /// 显示开屏图标。若自绘窗口无法创建，则回退到 WPF 自带的开屏窗口，避免影响启动流程。
    /// </summary>
    public static StartupSplash ShowSplash()
    {
        var splash = new StartupSplash();
        SplashWindow? window = null;
        try
        {
            window = new SplashWindow();
            window.Show();
            window.ForceRender();
            splash._window = window;
            return splash;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "显示开屏图标失败，已回退到系统开屏窗口");
            try
            {
                window?.Close();
            }
            catch
            {
                // 窗口可能尚未创建成功，忽略
            }
        }

        try
        {
            var fallback = new System.Windows.SplashScreen(@"Images\icon.png");
            fallback.Show(false, true);
            splash._fallbackSplash = fallback;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "显示系统开屏窗口失败，已跳过开屏图标");
        }

        return splash;
    }

    /// <summary>
    /// 淡出并关闭开屏图标，参数与 <see cref="System.Windows.SplashScreen.Close(TimeSpan)"/> 保持一致。
    /// </summary>
    /// <param name="fadeOutDuration">淡出时长。</param>
    public void Close(TimeSpan fadeOutDuration)
    {
        try
        {
            _window?.CloseAfterFade(fadeOutDuration);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "关闭开屏图标失败");
        }

        try
        {
            _fallbackSplash?.Close(fadeOutDuration);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "关闭系统开屏窗口失败");
        }
    }

    /// <summary>
    /// 开屏图标窗口。
    /// </summary>
    private sealed class SplashWindow : Window
    {
        private bool _isClosed;

        public SplashWindow()
        {
            // 无边框 + 透明背景，只显示图标本身
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Focusable = false;
            IsHitTestVisible = false;
            UseLayoutRounding = true;
            Width = IconSize;
            Height = IconSize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var image = new Image
            {
                Source = LoadIcon(),
                Stretch = Stretch.Uniform
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            Content = image;
        }

        private static BitmapSource LoadIcon()
        {
            var icon = new BitmapImage();
            icon.BeginInit();
            icon.UriSource = new Uri(IconUri, UriKind.Absolute);
            icon.CacheOption = BitmapCacheOption.OnLoad;
            icon.EndInit();
            icon.Freeze();
            return icon;
        }

        /// <summary>
        /// 立即完成一次布局与渲染。<br/>
        /// <see cref="Window.Show"/> 只是创建窗口，真正绘制需要等消息循环空闲，
        /// 而后续初始化会长时间阻塞 UI 线程，因此这里主动处理一次，保证图标马上出现在屏幕上。
        /// </summary>
        public void ForceRender()
        {
            UpdateLayout();
            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        }

        /// <summary>
        /// 淡出后关闭窗口。
        /// </summary>
        /// <param name="fadeOutDuration">淡出时长。</param>
        public void CloseAfterFade(TimeSpan fadeOutDuration)
        {
            if (_isClosed) return;

            if (!IsVisible || fadeOutDuration <= TimeSpan.Zero)
            {
                Close();
                return;
            }

            var animation = new DoubleAnimation(0d, fadeOutDuration);
            animation.Completed += (_, _) =>
            {
                if (!_isClosed) Close();
            };
            BeginAnimation(OpacityProperty, animation);
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            base.OnClosed(e);
        }
    }
}
