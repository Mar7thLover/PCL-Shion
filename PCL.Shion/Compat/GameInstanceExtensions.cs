using System;
using System.Runtime.CompilerServices;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Localization;

namespace PCL.Shion;

/// <summary>
/// 为 <see cref="GameInstance"/> 补充 WPF 版 McInstance 的部分成员。
/// </summary>
public static class GameInstanceExtensions
{
    // 解析版本 JSON 有一定开销，按实例对象缓存；重新扫描得到的实例是新对象，会自动重新解析
    private static readonly ConditionalWeakTable<GameInstance, McInstanceInfo> InfoCache = new();

    extension(GameInstance instance)
    {
        /// <summary>实例的原版版本与附加组件信息。</summary>
        public McInstanceInfo Info => InfoCache.GetValue(instance, McInstanceInfo.From);

        /// <summary>
        /// 生成实例的默认描述，例如「正式版 1.20.1, Forge 47.2.0」。实例出错时返回错误信息。
        /// </summary>
        public string GetDefaultDescription()
        {
            if (instance.Error is { } error)
                return error;

            var info = instance.Info;
            var unknown = Lang.Text("Minecraft.Version.Unknown");

            // 加载器信息
            var loaderInfo = "";
            if (info.HasForge)
                loaderInfo += ", Forge" + (info.Forge == unknown ? "" : " " + info.Forge);
            if (info.HasNeoForge)
                loaderInfo += ", NeoForge" + (info.NeoForge == unknown ? "" : " " + info.NeoForge);
            if (info.HasCleanroom)
                loaderInfo += ", Cleanroom" + (info.Cleanroom == unknown ? "" : " " + info.Cleanroom);
            if (info.HasLabyMod)
                loaderInfo += ", LabyMod" + (info.LabyMod == unknown ? "" : " " + info.LabyMod);
            if (info.HasFabric)
                loaderInfo += ", Fabric" + (info.Fabric == unknown ? "" : " " + info.Fabric);
            if (info.HasQuilt)
                loaderInfo += ", Quilt" + (info.Quilt == unknown ? "" : " " + info.Quilt);
            if (info.HasLegacyFabric)
                loaderInfo += ", Legacy Fabric" + (info.LegacyFabric == unknown ? "" : " " + info.LegacyFabric);
            if (info.HasOptiFine)
                loaderInfo += ", OptiFine" + (info.OptiFine == unknown
                    ? ""
                    : " " + info.OptiFine.Replace("-", " ").Replace("_", " "));
            if (info.HasLiteLoader)
                loaderInfo += ", LiteLoader";

            // 基础信息：原版版本的类别
            var vanilla = info.VanillaName;
            string text;
            if (vanilla.Length == 0)
                text = Lang.Text("Select.Instance.Description.UnknownMcVersion");
            else if (!string.IsNullOrEmpty(McVersionClassifier.GetMcFoolName(vanilla)))
                text = Lang.Text("Select.Instance.Description.AprilFools", vanilla);
            else if (vanilla.Contains("pre", StringComparison.OrdinalIgnoreCase))
                text = Lang.Text("Select.Instance.Description.PreRelease", vanilla);
            else if (vanilla.Contains("rc", StringComparison.OrdinalIgnoreCase))
                text = Lang.Text("Select.Instance.Description.ReleaseCandidate", vanilla);
            else if (vanilla.Contains("experimental", StringComparison.Ordinal))
                text = Lang.Text("Select.Instance.Description.ExperimentalSnapshot", vanilla);
            else if (vanilla == "pending")
                text = Lang.Text("Select.Instance.Description.ExperimentalSnapshot.Pending");
            else if (IsSnapshot(vanilla, instance.Name))
                text = Lang.Text("Select.Instance.Description.Snapshot", vanilla.Replace("-snapshot", ""));
            else
                text = Lang.Text("Select.Instance.Description.Release", vanilla);

            return (text + loaderInfo).Replace("_", "-");
        }
    }

    // 快照判断与 WPF 的 IsSnapshot 相同，只看版本名和实例名（版本 JSON 的 type 字段已由 pending 分支覆盖）
    private static bool IsSnapshot(string vanilla, string name)
    {
        foreach (var marker in new[] { "w", "snapshot", "rc", "pre", "experimental", "-" })
        {
            if (vanilla.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return name.Contains("combat", StringComparison.OrdinalIgnoreCase);
    }
}
