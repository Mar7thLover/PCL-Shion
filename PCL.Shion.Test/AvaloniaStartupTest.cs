using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Controls;
using PCL.Shion.Core.Accounts;
using PCL.Shion.Pages.Instance;
using PCL.Shion.Pages.Launch;
using PCL.Shion.Pages.Setup;
using PCL.Shion.Services;

namespace PCL.Shion.Test;

[TestClass]
[DoNotParallelize]
public sealed class AvaloniaStartupTest
{
    [TestMethod]
    public void RealApplicationCreatesMainWindowAndSettingsPagesWithoutWpf()
    {
        // 不打开窗口，避免触发首页联网、账号访问与外部进程；仍加载实际应用的 XAML、主题与控件。
        var directory = Path.Combine(Path.GetTempPath(), "shion-headless-" + Guid.NewGuid().ToString("N"));
        var loader = App.SettingsLoader;
        App.SettingsLoader = () => AppSettings.Load(Path.Combine(directory, "settings.json"));
        var lifetime = new ClassicDesktopStyleApplicationLifetime { ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown };
        try
        {
            AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithLifetime(lifetime);
            Assert.IsInstanceOfType<MainWindow>(lifetime.MainWindow);
            Assert.IsNotNull(lifetime.MainWindow.Content);
            lifetime.MainWindow.Measure(new Size(900, 550));
            lifetime.MainWindow.Arrange(new Rect(0, 0, 900, 550));
            Assert.IsTrue(lifetime.MainWindow.Bounds.Width > 0);
            Assert.IsNotNull(new PageSetupLaunch().Content);
            Assert.IsNotNull(new PageSetupLog().Content);
            Assert.IsNotNull(new PageSetupLauncherMisc().Content);
            var overall = new PageInstanceOverall();
            Assert.IsFalse(overall.FindControl<MyButton>("BtnManageScript")!.IsEnabled);
            Assert.IsFalse(overall.FindControl<MyButton>("BtnManageTest")!.IsEnabled);
            AssertEscapeCancelsLoginOnce(lifetime.MainWindow);
        }
        finally
        {
            App.Services?.Dispose();
            App.SettingsLoader = loader;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static void AssertEscapeCancelsLoginOnce(Window window)
    {
        var completions = 0;
        var converter = new MyMsgLoginConverter
        {
            Device = new DeviceCodeInfo("unused-device", "unused-code", "https://example.invalid/verify", null, 300, 5),
            DeviceCodePoll = _ => throw new InvalidOperationException("Canceled login must not poll the network."),
            CompletionHandler = _ => completions++
        };
        var dialog = new MyMsgLogin(converter);
        var panel = window.FindControl<Panel>("PanMsg")!;
        panel.Children.Add(dialog);
        try
        {
            var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };
            window.RaiseEvent(key);
            Assert.IsTrue(key.Handled);
            Assert.IsTrue(converter.IsExited);
            Assert.IsInstanceOfType<OperationCanceledException>(converter.Result);
            Assert.AreEqual(1, completions);
            dialog.Cancel();
            dialog.OpenVerificationWebsite(); // Closed dialogs must never reopen an external browser.
            Assert.AreEqual(1, completions);
        }
        finally
        {
            dialog.Cancel();
            panel.Children.Remove(dialog);
        }
    }
}
