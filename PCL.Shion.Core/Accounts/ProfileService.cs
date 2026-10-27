using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Shion.Core.Platform;
using PCL.Shion.Core.IO;

namespace PCL.Shion.Core.Accounts;

/// <summary>
/// 档案列表与当前选中的档案，保存在 <c>PCL Shion/profiles.json</c>（与设置文件位于同一目录）。
/// 所有方法都是线程安全的；返回给调用方的档案都是副本。
/// </summary>
public static class ProfileService
{
    private static readonly Lock Gate = new();
    private static readonly List<McProfile> Items = [];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static bool _loaded;
    private static bool _canPersist = true;
    private static string? _selectedId;

    /// <summary>启动器数据目录，与设置文件位于同一目录。</summary>
    public static string DataDirectory { get; } = Path.Combine(
        SpecialFolders.Get(Environment.SpecialFolder.ApplicationData), "PCL Shion");

    private static string FilePath => Path.Combine(DataDirectory, "profiles.json");

    /// <summary>
    /// 是否正在新建档案。新建过程中，启动器的档案页会显示对应的登录页面。
    /// </summary>
    public static bool IsCreatingProfile { get; set; }

    /// <summary>所有档案，按添加顺序排列。</summary>
    public static IReadOnlyList<McProfile> Profiles
    {
        get
        {
            lock (Gate)
            {
                EnsureLoaded();
                return Items.Select(profile => profile.Clone()).ToArray();
            }
        }
    }

    /// <summary>当前选中的档案；没有选中时为 null。</summary>
    public static McProfile? Current
    {
        get
        {
            lock (Gate)
            {
                EnsureLoaded();
                return Items.FirstOrDefault(profile => profile.ProfileId == _selectedId)?.Clone();
            }
        }
    }

    /// <summary>是否已经添加过微软正版档案。</summary>
    public static bool HasMicrosoftProfile => Profiles.Any(profile => profile.ProfileType == ProfileType.Microsoft);

    /// <summary>从磁盘重新读取档案列表。</summary>
    public static void Load()
    {
        lock (Gate)
            LoadUnsafe();
    }

    public static void Add(McProfile profile, bool select = true)
    {
        lock (Gate)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(profile.ProfileId))
                profile.ProfileId = Guid.NewGuid().ToString("N");
            Items.Add(profile.Clone());
            if (select)
                _selectedId = profile.ProfileId;
            Save();
        }
    }

    /// <summary>用 <paramref name="updated"/> 替换 <paramref name="origin"/>，两者按 <see cref="McProfile.ProfileId"/> 对应。</summary>
    public static void Update(McProfile origin, McProfile updated)
    {
        lock (Gate)
        {
            EnsureLoaded();
            var index = Items.FindIndex(profile => profile.ProfileId == origin.ProfileId);
            if (index < 0)
                return;
            var replacement = updated.Clone();
            replacement.ProfileId = origin.ProfileId;
            Items[index] = replacement;
            Save();
        }
    }

    public static void Remove(McProfile profile)
    {
        lock (Gate)
        {
            EnsureLoaded();
            Items.RemoveAll(item => item.ProfileId == profile.ProfileId);
            if (_selectedId == profile.ProfileId)
                _selectedId = null;
            Save();
        }
    }

    public static void Select(McProfile? profile)
    {
        lock (Gate)
        {
            EnsureLoaded();
            _selectedId = profile?.ProfileId;
            Save();
        }
    }

    /// <summary>
    /// 把一次登录的结果写入档案。<paramref name="existing"/> 为 null 时新建档案，否则更新该档案。
    /// </summary>
    /// <returns>写入后的档案。</returns>
    public static McProfile ApplyLogin(AccountLoginResult result, McProfile? existing, bool select)
    {
        var profile = existing?.Clone() ?? new McProfile();
        profile.ProfileType = result.ProfileType;
        profile.UserName = result.UserName;
        profile.Uuid = result.Uuid;
        profile.AccessToken = result.AccessToken;
        profile.RefreshToken = result.RefreshToken;
        profile.IdToken = result.IdToken ?? profile.IdToken;
        profile.ClientToken = result.ClientToken;
        profile.ExpiresAt = result.ExpiresAt;
        profile.Server = result.Server ?? profile.Server;
        profile.ServerName = result.ServerName ?? profile.ServerName;
        profile.LoginName = result.LoginName ?? profile.LoginName;
        profile.DiscoveryAddress = result.DiscoveryAddress ?? profile.DiscoveryAddress;
        profile.ClientId = result.ClientId ?? profile.ClientId;

        if (existing is null)
        {
            Add(profile, select);
        }
        else
        {
            Update(existing, profile);
            if (select)
                Select(profile);
        }

        return profile;
    }

    private static void EnsureLoaded()
    {
        if (!_loaded)
            LoadUnsafe();
    }

    private static void LoadUnsafe()
    {
        Items.Clear();
        _selectedId = null;
        _loaded = true;
        _canPersist = true;
        var result = JsonFileStore.Read<StoredProfiles>(FilePath, JsonOptions);
        _canPersist = result.CanPersist;
        if (result.Value is { } stored)
        {
            if (stored.Profiles is not null)
                Items.AddRange(stored.Profiles.Where(profile => profile is not null));
            _selectedId = stored.SelectedProfileId;
        }
    }

    private static void Save()
    {
        if (!_canPersist)
            return;

        try
        {
            JsonFileStore.Save(FilePath, new StoredProfiles(_selectedId, Items), JsonOptions);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 档案只是便利数据，写入失败不应影响启动器运行
        }
    }

    private sealed record StoredProfiles(string? SelectedProfileId, List<McProfile>? Profiles);
}
