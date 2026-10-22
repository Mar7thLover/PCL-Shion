#if DEBUG
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PCL;

/// <summary>
///     调试工具：设置环境变量 PCL_DEBUG_SNAPSHOT_DIR 后，监视该目录中的 *.cmd 文件并按行执行命令，
///     截图只渲染启动器自身的窗口，不会截到其他程序。
///     命令：page &lt;PageType&gt; [PageSubType] | news &lt;序号&gt; | size &lt;宽&gt; &lt;高&gt; | msgbox-ok | wait &lt;毫秒&gt; | shot &lt;文件名&gt;
/// </summary>
internal static class DebugSnapshot
{
    private static FileSystemWatcher? _watcher;

    public static void StartIfRequested()
    {
        var dir = Environment.GetEnvironmentVariable("PCL_DEBUG_SNAPSHOT_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        _watcher = new FileSystemWatcher(dir, "*.cmd") { EnableRaisingEvents = true };
        _watcher.Created += (_, e) => ModBase.RunInNewThread(() => Run(e.FullPath, dir), "Debug Snapshot");
        _watcher.Renamed += (_, e) =>
        {
            if (e.FullPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
                ModBase.RunInNewThread(() => Run(e.FullPath, dir), "Debug Snapshot");
        };
        ModBase.Log($"[Snapshot] 正在监视 {dir}");
    }

    private static void Run(string cmdFile, string dir)
    {
        string[] lines;
        try
        {
            Thread.Sleep(200);
            lines = File.ReadAllLines(cmdFile);
            File.Delete(cmdFile);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Snapshot] 读取命令文件失败");
            return;
        }

        foreach (var raw in lines)
        {
            var args = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0) continue;
            try
            {
                if (args[0] == "wait")
                {
                    Thread.Sleep(int.Parse(args[1]));
                    continue;
                }
                ModBase.RunInUiWait(() => Execute(args, dir));
                Thread.Sleep(300);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[Snapshot] 执行命令失败：{raw}");
            }
        }
        File.WriteAllText(Path.Combine(dir, Path.GetFileNameWithoutExtension(cmdFile) + ".done"), "ok");
    }

    private static void Execute(string[] args, string dir)
    {
        var main = ModMain.frmMain;
        switch (args[0])
        {
            case "page":
            {
                var page = Enum.Parse<FormMain.PageType>(args[1]);
                var sub = args.Length > 2 ? Enum.Parse<FormMain.PageSubType>(args[2]) : FormMain.PageSubType.Default;
                if (page == FormMain.PageType.Tools && args.Length > 2)
                {
                    main.PageChange(FormMain.PageType.Tools);
                    ModMain.frmToolsLeft?.OpenSubPage(sub);
                }
                else
                {
                    main.PageChange(page, sub);
                }
                break;
            }
            case "news":
            {
                var items = (ModMain.frmToolsNews?.DataContext as PCL.Core.ViewModel.Homepage.NewsViewModel)?.NewsItems;
                var item = items?.ElementAtOrDefault(args.Length > 1 ? int.Parse(args[1]) : 0);
                if (item is null) ModBase.Log("[Snapshot] 没有可打开的新闻，先打开 Tools ToolsNews 并等待加载");
                else PageHomepageNewsView.OpenNewsDetail(item);
                break;
            }
            case "size":
                main.Width = double.Parse(args[1]);
                main.Height = double.Parse(args[2]);
                break;
            case "msgbox-ok":
                FindDescendant<MyMsgText>(main)?.Btn1_Click();
                break;
            case "shot":
            {
                var root = (FrameworkElement)main.Content;
                var dpi = VisualTreeHelper.GetDpi(root);
                var bmp = new RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX), (int)(root.ActualHeight * dpi.DpiScaleY),
                    dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                bmp.Render(root);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using var stream = File.Create(Path.Combine(dir, args[1] + ".png"));
                encoder.Save(stream);
                ModBase.Log($"[Snapshot] 已保存 {args[1]}.png");
                break;
            }
            default:
                ModBase.Log($"[Snapshot] 未知命令：{args[0]}");
                break;
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var deeper = FindDescendant<T>(child);
            if (deeper is not null) return deeper;
        }
        return null;
    }
}
#endif
