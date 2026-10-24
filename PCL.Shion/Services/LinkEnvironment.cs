using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using PCL.Shion.Core.Link;
using PCL.Shion.Core.Platform;
using PCL.Shion.Localization;
using Lang = PCL.Shion.Localization.Lang;
using PCL.Shion.UI;
using CoreHintTheme = PCL.Shion.Core.Link.HintTheme;
using CoreProtocol = PCL.Shion.Core.Link.LinkProtocolPreference;

namespace PCL.Shion.Services;

/// <summary>
///     把联机组件（PCL.Shion.Core.Link）接到 Shion 的设置、日志、语言与提示上。
///     WPF 版的联机代码直接使用 Config、States、Lang 等全局入口，这里提供同样的入口。
/// </summary>
public static class LinkEnvironment
{
    private static bool _initialized;

    public static void Initialize(AppSettings settings)
    {
        if (_initialized) return;
        _initialized = true;

        LinkHost.Settings = new LinkSettingsAdapter(settings);
        LinkHost.VersionName = AppInfo.VersionText;
        LinkHost.DataDirectory = Path.Combine(
            SpecialFolders.Get(Environment.SpecialFolder.LocalApplicationData), "PCL Shion", "Link");
        LinkHost.Text = static (key, args) => args.Length == 0 ? Lang.Text(key) : Lang.Text(key, args);
        LinkHost.Hint = static (text, theme) => Dispatcher.UIThread.Post(() => HintService.Hint(text, theme switch
        {
            CoreHintTheme.Success => HintType.Success,
            CoreHintTheme.Error => HintType.Error,
            _ => HintType.Info
        }));
        LinkHost.RunInUi = static action =>
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return Dispatcher.UIThread.InvokeAsync(action).GetTask();
        };
        LinkHost.Log = static (level, text, ex) =>
        {
            switch (level)
            {
                case LinkLogLevel.Debug:
                    LogWrapper.Debug(ex is null ? text : $"{text}：{ex}");
                    break;
                case LinkLogLevel.Info:
                    LogWrapper.Info(text);
                    break;
                case LinkLogLevel.Warn:
                    LogWrapper.Warn(ex, text);
                    break;
                default:
                    LogWrapper.Error(ex, text);
                    break;
            }
        };
    }

    /// <summary>
    ///     联机设置与状态的适配。状态类的项（公告缓存、令牌等）在赋值时立即写盘，
    ///     与 WPF 的 States 行为一致；设置类的项由设置页自行保存。
    /// </summary>
    private sealed class LinkSettingsAdapter(AppSettings settings) : ILinkSettings
    {
        public string Username
        {
            get => settings.LinkUsername;
            set => settings.LinkUsername = value;
        }

        public LinkRelayBehavior RelayType
        {
            get => settings.LinkRelayType;
            set => settings.LinkRelayType = value;
        }

        public int ServerType
        {
            get => settings.LinkServerType;
            set => settings.LinkServerType = value;
        }

        public bool UseLatencyFirstMode
        {
            get => settings.LinkLatencyFirstMode;
            set => settings.LinkLatencyFirstMode = value;
        }

        public string CustomRelayServer
        {
            get => settings.LinkRelayServer;
            set => settings.LinkRelayServer = value;
        }

        public CoreProtocol ProtocolPreference
        {
            get => (CoreProtocol)(int)settings.LinkProtocolPreference;
            set => settings.LinkProtocolPreference = (LinkProtocolPreference)(int)value;
        }

        public bool TryPunchSym
        {
            get => settings.LinkTryPunchSym;
            set => settings.LinkTryPunchSym = value;
        }

        public bool EnableIPv6
        {
            get => settings.LinkEnableIPv6;
            set => settings.LinkEnableIPv6 = value;
        }

        public bool EnableCliOutput
        {
            get => settings.LinkEnableCliOutput;
            set => settings.LinkEnableCliOutput = value;
        }

        public bool LinkEula
        {
            get => settings.LinkEula;
            set => SaveAfter(() => settings.LinkEula = value);
        }

        public string AnnounceCache
        {
            get => settings.LinkAnnounceCache;
            set => settings.LinkAnnounceCache = value;
        }

        public int AnnounceCacheVer
        {
            get => settings.LinkAnnounceCacheVer;
            set => settings.LinkAnnounceCacheVer = value;
        }

        public string NaidRefreshToken
        {
            get => settings.LinkNaidRefreshToken;
            set => SaveAfter(() => settings.LinkNaidRefreshToken = value);
        }

        public string NaidRefreshExpireTime
        {
            get => settings.LinkNaidRefreshExpiresAt;
            set => SaveAfter(() => settings.LinkNaidRefreshExpiresAt = value);
        }

        public bool DoFirstTimeNetTest
        {
            get => settings.LinkFirstTimeNetTest;
            set => SaveAfter(() => settings.LinkFirstTimeNetTest = value);
        }

        public void Save() => settings.Save();

        private void SaveAfter(Action assign)
        {
            assign();
            try
            {
                settings.Save();
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "[Link] 保存联机状态失败");
            }
        }
    }
}
