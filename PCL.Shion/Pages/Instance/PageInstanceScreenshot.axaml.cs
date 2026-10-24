using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Shion.Controls;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using PCL.Shion.Navigation;
using PCL.Shion.Pages;
using PCL.Shion.Services;
using PCL.Shion.UI;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Pages.Instance;

public partial class PageInstanceScreenshot : MyPageRight
{
    private bool _AppendLock;
    private int _Offset;

    private List<string> fileList = new();

    private bool isLoad;
    private string screenshotPath = "";

    public PageInstanceScreenshot()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Loaded += PageSetupLaunch_Loaded;
        PanBack.ScrollChanged += RequireAppend;
        BtnOpenFolder.Click += BtnOpenFolder_Click;
        BtnOpenFolderTop.Click += BtnOpenFolder_Click;
    }

    /// <summary>
    ///     刷新截图页面（供外部调用，等同于 WPF 版 IRefreshable.Refresh）。
    /// </summary>
    public void RefreshSelf()
    {
        _ = RefreshAsync();
    }

    public static async Task RefreshAsync()
    {
        if (Navigator.PageRight is PageInstanceScreenshot screenshot)
            await screenshot.ReloadAsync();
        if (PageInstanceLeft.frmInstanceLeft is { } left)
            left.ItemScreenshot.Checked = true;
        Hint.Show(Lang.Text("Instance.Saves.Status.Refreshing"));
    }

    private void PageSetupLaunch_Loaded(object? sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        screenshotPath = PageInstanceLeft.McInstance.PathIndie + "screenshots" + Path.DirectorySeparatorChar;
        if (!Directory.Exists(screenshotPath))
            Directory.CreateDirectory(screenshotPath);
        Dispatcher.UIThread.Post(() => _ = ReloadAsync());

        // 非重复加载部分
        if (isLoad)
            return;
        isLoad = true;
    }

    /// <summary>
    ///     确保当前页面上的信息已正确显示。
    /// </summary>
    public async Task ReloadAsync()
    {
        ModAnimation.AniControlEnabled += 1;
        PanBack.ScrollToHome();
        await LoadFileListAsync();
        ModAnimation.AniControlEnabled -= 1;
    }

    private void RefreshTip()
    {
        if (fileList.Count.Equals(0))
        {
            PanNoPic.IsVisible = true;
            PanContent.IsVisible = false;
        }
        else
        {
            PanNoPic.IsVisible = false;
            PanContent.IsVisible = true;
        }
    }

    private static readonly string[] allowedSuffix = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.tiff"];

    private async Task LoadFileListAsync()
    {
        ModBase.Log("[Screenshot] 刷新截图文件");
        fileList.Clear();
        if (Directory.Exists(screenshotPath))
        {
            fileList = allowedSuffix
                .SelectMany(suffix => Directory.EnumerateFiles(screenshotPath, suffix, SearchOption.TopDirectoryOnly))
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();
        }

        PanList.Children.Clear();
        RefreshTip();
        ModBase.Log("[Screenshot] 共发现 " + fileList.Count + " 个截图文件");
        if (fileList.Count == 0)
            return;
        await ListAppendAsync(20, 0);
    }

    private void RequireAppend(object? sender, ScrollChangedEventArgs e)
    {
        if (fileList.Count != 0 && !_AppendLock && PanBack.Offset.Y + PanBack.Viewport.Height >= PanBack.Extent.Height)
        {
            Dispatcher.UIThread.Post(() => _ = ListAppendAsync());
        }
    }

    private async Task ListAppendAsync(int count = 20, int offset = -1)
    {
        _AppendLock = true;
        if (offset == -1)
        {
            if (_Offset * count > fileList.Count)
            {
                _AppendLock = false;
                return;
            }

            offset = _Offset + 1;
            _Offset += 1;
        }
        else
        {
            _Offset = offset;
        }

        if (count * offset > fileList.Count)
        {
            _AppendLock = false;
            return;
        }

        for (int j = count * offset, loopTo = count * (offset + 1) - 1; j <= loopTo; j++)
        {
            if (j >= fileList.Count)
                break;
            var i = fileList.ElementAt(j);
            try
            {
                if (!File.Exists(i))
                    continue; // 文件在加载途中消失了
                if (File.GetAttributes(i).HasFlag(FileAttributes.Hidden))
                    continue; // 隐藏文件
                if (new FileInfo(i).Length == 0L)
                    continue; // 空文件
                var myCard = new MyCard
                {
                    Margin = new Thickness(7),
                    Tag = i
                };
                ToolTip.SetTip(myCard, i.Replace(screenshotPath, "")); // 适配高清截图模组
                var grid = new Grid();
                myCard.Children.Add(grid);

                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(9d) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(120d) });
                grid.RowDefinitions.Add(new RowDefinition());

                // 图片
                var image = new Image();
                image.Source = await Task.Run(() =>
                {
                    using var fs = new FileStream(i, FileMode.Open, FileAccess.Read);
                    return Bitmap.DecodeToWidth(fs, 400);
                });
                image.Stretch = Stretch.Uniform; // 使图片自适应控件大小
                image.Cursor = new Cursor(StandardCursorType.Hand);
                image.PointerPressed += (_, ev) =>
                {
                    if (!ev.GetCurrentPoint(image).Properties.IsLeftButtonPressed)
                        return;
                    try
                    {
                        SystemShell.OpenFolder(i);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(
                            ex,
                            Lang.Text("Instance.Screenshot.OpenFailed"),
                            ModBase.LogLevel.Hint,
                            userSummary: Lang.Text("Instance.Screenshot.OpenFailed"));
                    }
                }; // 使用系统默认程序打开
                Grid.SetRow(image, 1);
                grid.Children.Add(image);

                // 按钮
                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(3d, 5d, 3d, 5d)
                };
                Grid.SetRow(stackPanel, 2);
                grid.Children.Add(stackPanel);

                var btnOpen = new MyIconTextButton
                {
                    Name = "BtnOpen",
                    Text = Lang.Text("Common.Action.Open"),
                    LogoScale = 0.8d,
                    SvgIcon = "lucide/folder-open",
                    Tag = i
                };
                btnOpen.Click += (s, ev) => BtnOpen_Click((MyIconTextButton)s!, ev);
                stackPanel.Children.Add(btnOpen);
                var btnDelete = new MyIconTextButton
                {
                    Name = "BtnDelete",
                    Text = Lang.Text("Common.Action.Delete"),
                    LogoScale = 0.8d,
                    SvgIcon = "lucide/trash-2",
                    Tag = i
                };
                btnDelete.Click += (s, ev) => BtnDelete_Click((MyIconTextButton)s!, ev);
                stackPanel.Children.Add(btnDelete);
                var btnCopy = new MyIconTextButton
                {
                    Name = "BtnCopy",
                    Text = Lang.Text("Common.Action.Copy"),
                    LogoScale = 0.8d,
                    SvgIcon = "lucide/copy",
                    Tag = i
                };
                btnCopy.Click += (s, ev) => BtnCopy_Click((MyIconTextButton)s!, ev);
                stackPanel.Children.Add(btnCopy);
                PanList.Children.Add(myCard);
                myCard.Opacity = 0d;
                ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(myCard, 1d, 200) });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[Screenshot] 创建 {i} 截图预览失败，图像可能损坏");
            }
        }

        _AppendLock = false;
    }

    private void RemoveItem(string path)
    {
        try
        {
            foreach (var i in PanList.Children.OfType<MyCard>())
                if (Equals(i.Tag, path))
                {
                    PanList.Children.Remove(i);
                    break;
                }

            fileList.Remove(path);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "未能找到对应 UI");
        }
    }

    private static string GetPathFromSender(MyIconTextButton sender)
    {
        return (string)sender.Tag!;
    }

    private void BtnOpen_Click(MyIconTextButton sender, RouteEventArgs e)
    {
        SystemShell.OpenFolder(GetPathFromSender(sender));
    }

    private void BtnDelete_Click(MyIconTextButton sender, RouteEventArgs e)
    {
        var path = GetPathFromSender(sender);
        try
        {
            Trash.MoveToTrash(path);
            RemoveItem(path);
            RefreshTip();
            Hint.Show(Lang.Text("Instance.Screenshot.Deleted"));
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Instance.Screenshot.DeleteFailed"),
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Screenshot.DeleteFailed"));
        }
    }

    private async void BtnCopy_Click(MyIconTextButton sender, RouteEventArgs e)
    {
        var imagePath = GetPathFromSender(sender);
        if (!File.Exists(imagePath))
        {
            Hint.Show(Lang.Text("Instance.Screenshot.FileNotFound"));
            return;
        }

        var tryTime = 0;
        while (tryTime <= 5)
            try
            {
                ModBase.Log("[Screenshot] 尝试复制" + imagePath + "到剪贴板");
                var topLevel = TopLevel.GetTopLevel(this)
                               ?? throw new InvalidOperationException("TopLevel is unavailable.");
                var clipboard = topLevel.Clipboard
                                ?? throw new InvalidOperationException("Clipboard is unavailable.");
                var file = await topLevel.StorageProvider.TryGetFileFromPathAsync(imagePath)
                           ?? throw new FileNotFoundException("无法访问截图文件", imagePath);
                await clipboard.SetFileAsync(file);
                Hint.Show(Lang.Text("Instance.Screenshot.CopiedToClipboard"));
                return;
            }
            catch (Exception ex)
            {
                tryTime += 1;
                ModBase.Log(ex, $"[Screenshot]第 {tryTime} 次复制尝试失败");
            }

        Hint.Show(Lang.Text("Instance.Screenshot.CopyFailed"), HintType.Error);
    }

    private void BtnOpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(screenshotPath))
            Directory.CreateDirectory(screenshotPath);
        SystemShell.OpenFolder(screenshotPath);
    }
}
