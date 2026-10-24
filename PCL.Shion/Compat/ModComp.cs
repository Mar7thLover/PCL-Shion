using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PCL.Shion.Core.Online;

namespace PCL.Shion;

/// <summary>
///     在线资源（Mod / 整合包等）的全局缓存，对应 WPF 的 ModComp 静态缓存。
///     缓存实际存放在 PCL.Shion.Core 中，这里只是转发引用，清空缓存等同于清空 Core 的缓存。
/// </summary>
public static class ModComp
{
    /// <summary>工程信息缓存，键为工程 ID。</summary>
    public static readonly ConcurrentDictionary<string, CompProject> compProjectCache = CompProject.Cache;

    /// <summary>文件列表缓存，键为工程 ID。</summary>
    public static readonly ConcurrentDictionary<string, List<CompFile>> compFilesCache = CompApi.FilesCache;

    /// <summary>
    ///     收藏分享码的编解码，对应 WPF 版 ModComp.CompFavorites 中的分享码部分。
    ///     分享码为收藏工程 ID 集合的 JSON 字符串。
    /// </summary>
    public static class CompFavorites
    {
        private static readonly JsonSerializerOptions ShareOptions = new() { PropertyNameCaseInsensitive = true };

        /// <summary>生成分享码，出错时返回空字符串。</summary>
        public static string GetShareCode(IEnumerable<string> data)
        {
            try
            {
                return JsonSerializer.Serialize(data.ToList(), ShareOptions);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[CompFavorites] 生成分享出错");
            }

            return "";
        }

        /// <summary>通过分享码解析出工程 ID 集合，出错时返回空集合。</summary>
        public static HashSet<string> GetIdsByShareCode(string code)
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(code, ShareOptions)?.ToHashSet() ?? new HashSet<string>();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[CompFavorites] 通过分享获取 ID 出错");
            }

            return new HashSet<string>();
        }
    }
}
