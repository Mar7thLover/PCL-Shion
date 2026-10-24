using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using PCL.Shion.Controls;

namespace PCL.Shion;

/// <summary>
///     与 WPF 版 PCL.Core.UI.ImageLoaderHelper 对应的图像加载辅助。
///     Base64 图像先解码、校验并缓存为本地文件，再交给 <see cref="MyImage" /> 按文件路径加载，解码失败时使用后备图像。
/// </summary>
public static class ImageLoaderHelper
{
    private const string DefaultServerImage = "avares://PCL.Shion/Assets/Images/Icons/DefaultServer.png";

    private static readonly string CacheFolder = Path.Combine(Path.GetTempPath(), "PCL Shion", "ServerIcons");

    /// <summary>
    ///     异步设置服务器图标。
    /// </summary>
    /// <param name="base64String">Base64 图像字符串，可带 <c>data:image/...;base64,</c> 前缀。</param>
    /// <param name="imageElement">目标图像控件。</param>
    /// <param name="defaultImageUri">加载失败时使用的默认图像，为空时使用内置的默认服务器图标。</param>
    public static Task SetServerLogoAsync(string? base64String, MyImage imageElement, string? defaultImageUri = null)
    {
        return SetImageFromBase64Async(base64String, imageElement, defaultImageUri ?? DefaultServerImage);
    }

    /// <summary>
    ///     异步把 Base64 图像设置到控件上。空值或解码失败时设置为后备图像。
    /// </summary>
    /// <param name="base64String">Base64 图像字符串。</param>
    /// <param name="imageElement">目标图像控件。</param>
    /// <param name="fallbackImageUri">加载失败时的后备图像地址。</param>
    public static async Task SetImageFromBase64Async(string? base64String, MyImage imageElement,
        string? fallbackImageUri = null)
    {
        ArgumentNullException.ThrowIfNull(imageElement);

        if (string.IsNullOrWhiteSpace(base64String))
        {
            imageElement.Source = fallbackImageUri;
            return;
        }

        try
        {
            var path = await Task.Run(() => SaveBase64Image(base64String));
            imageElement.Source = path;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Base64 图像加载失败，已改用后备图像");
            imageElement.Source = fallbackImageUri;
        }
    }

    /// <summary>
    ///     解码 Base64 图像，确认其为有效图像后写入缓存文件，返回缓存文件路径。
    /// </summary>
    private static string SaveBase64Image(string base64String)
    {
        var comma = base64String.IndexOf(',');
        var base64Data = comma >= 0 ? base64String[(comma + 1)..] : base64String;
        var bytes = Convert.FromBase64String(base64Data);

        // 先完整解码一次，无效图像在这里抛出异常，与 WPF 版的 _CreateBitmapFromBase64 行为一致
        using (var decoded = new Bitmap(new MemoryStream(bytes)))
        {
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Directory.CreateDirectory(CacheFolder);
        var path = Path.Combine(CacheFolder, hash + ".png");
        if (!File.Exists(path))
            File.WriteAllBytes(path, bytes);
        return path;
    }
}
