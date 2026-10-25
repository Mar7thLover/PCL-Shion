using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using PCL.Shion.Core.Minecraft;
using PCL.Shion.Core.Mods;
using PCL.Shion.Core.Online;
using PCL.Shion.Core.Worlds.Nbt;
using PCL.Shion.Navigation;
using PCL.Shion.Pages.Instance;

namespace PCL.Shion;

/// <summary>
///     实例资源目录（Mod、资源包、光影包、投影原理图）中已安装文件的加载与查找，对应 WPF 的 ModLocalComp。
///     文件读取与元数据解析由 PCL.Shion.Core 的 <see cref="ModScanner"/> 完成。
/// </summary>
public static class ModLocalComp
{
    /// <summary>
    ///     资源列表加载器的输入参数。
    /// </summary>
    public sealed class CompLocalLoaderData
    {
        /// <summary>所属的游戏实例。</summary>
        public GameInstance? gameVersion;

        /// <summary>发起加载的页面。</summary>
        public object? frm;

        /// <summary>当前要列出的文件夹。</summary>
        public string compPath = "";

        /// <summary>资源类型。</summary>
        public CompType compType;

        /// <summary>该资源类型依赖的加载器。</summary>
        public List<CompLoaderType> loaders = [];
    }

    /// <summary>
    ///     内嵌（Jar-in-Jar）依赖是否已完成解析。
    ///     TODO(shion): Shion 尚未移植内嵌 Mod 的嵌套解析（WPF 版 ModLocalComp 的 JIJ 流程），当前恒为已完成且不含内嵌项。
    /// </summary>
    public static bool CompJijResolved = true;

    /// <summary>
    ///     当前正在进行的内嵌解析上下文，仅在解析期间非空。
    ///     TODO(shion): 与内嵌解析流程一同移植。
    /// </summary>
    public static ModJarInJar.ScanContext? JijScanContext;

    /// <summary>
    ///     资源列表加载器。输入为 <see cref="CompLocalLoaderData"/>，输出为当前文件夹中的资源列表。
    /// </summary>
    public static readonly ModLoader.LoaderTask<CompLocalLoaderData, List<LocalCompFile>> compResourceListLoader =
        new("ModLocalComp Resource List", LoadResourceList);

    /// <summary>
    ///     获取当前实例的版本支持的加载器，用于 Mod 的筛选。
    /// </summary>
    public static List<CompLoaderType> GetCurrentVersionModLoader()
    {
        var list = new List<CompLoaderType>();
        var info = PageInstanceLeft.McInstance?.Info;
        if (info is null)
            return list;
        if (info.HasForge)
            list.Add(CompLoaderType.Forge);
        if (info.HasNeoForge)
            list.Add(CompLoaderType.NeoForge);
        if (info.HasFabric)
            list.Add(CompLoaderType.Fabric);
        if (info.HasQuilt)
            list.Add(CompLoaderType.Quilt);
        if (info.HasLiteLoader)
            list.Add(CompLoaderType.LiteLoader);
        return list;
    }

    /// <summary>
    ///     获取资源类型对应的游戏目录子文件夹名称。
    /// </summary>
    public static string GetPathNameByCompType(CompType theType) => theType switch
    {
        CompType.Mod => "mods",
        CompType.ResourcePack => "resourcepacks",
        CompType.Shader => "shaderpacks",
        CompType.Schematic => "schematics",
        _ => "mods"
    };

    /// <summary>
    ///     按 Mod ID 在实例的 mods 目录中查找 Mod 文件。
    /// </summary>
    public static LocalCompFile? GetModLocalCompByKeywords(GameInstance instance, string? modId, string mainKeyword,
        params string[] keywords)
    {
        if (modId is null)
            return null;
        return GetModLocalCompByKeywords(instance, new[] { modId }, mainKeyword, keywords);
    }

    /// <summary>
    ///     在实例的 mods 目录中查找 Mod 文件。文件名需包含 <paramref name="mainKeyword"/>，
    ///     并且（若给出了 keywords）包含其中任意一个关键字，且 Mod ID 属于 <paramref name="modIds"/>。
    /// </summary>
    public static LocalCompFile? GetModLocalCompByKeywords(GameInstance instance, string[] modIds, string mainKeyword,
        params string[] keywords)
    {
        // 跳过不可安装 Mod 的实例（出错的实例）
        if (instance.Error is not null)
            return null;

        var modFolder = ModScanner.GetModsDirectory(instance.Directory);
        if (!Directory.Exists(modFolder))
            return null;

        foreach (var file in Directory.EnumerateFiles(modFolder, $"*{mainKeyword}*"))
        {
            var lowerFilePath = file.ToLowerInvariant();
            if (!LocalCompFile.IsModFileName(lowerFilePath))
                continue;
            if (keywords.Length > 0 && !keywords.Any(keyword => lowerFilePath.Contains(keyword, StringComparison.Ordinal)))
                continue;

            LocalCompFile candidate;
            try
            {
                candidate = new LocalCompFile(file);
            }
            catch (Exception)
            {
                // 无法读取的 jar 直接跳过，与 WPF 中忽略解析失败的文件保持一致
                continue;
            }

            if (candidate.State == LocalCompFile.LocalFileStatus.Unavailable)
                continue;
            if (modIds.Any(modId => (candidate.ModId ?? "") == (modId ?? "")))
                return candidate;
        }

        return null;
    }

    private static void LoadResourceList(ModLoader.LoaderTask<CompLocalLoaderData, List<LocalCompFile>> loader)
    {
        var result = new List<LocalCompFile>();
        var input = loader.input;
        if (input is null || string.IsNullOrEmpty(input.compPath) || !Directory.Exists(input.compPath))
        {
            loader.output = result;
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(input.compPath))
        {
            if (loader.IsAborted)
                return;
            result.Add(new LocalCompFile(Path.Combine(directory, LocalCompFile.FolderMarker)));
        }

        foreach (var file in Directory.EnumerateFiles(input.compPath))
        {
            if (loader.IsAborted)
                return;
            if (!LocalCompFile.IsCompFile(file, input.compType))
                continue;
            result.Add(new LocalCompFile(file));
        }

        ResolveComps(result);
        loader.output = result;
    }

    /// <summary>
    ///     通过本地已缓存的工程文件列表（<see cref="CompApi.FilesCache"/>）按 SHA-1 关联在线工程，不发起网络请求。
    /// </summary>
    private static void ResolveComps(IEnumerable<LocalCompFile> items)
    {
        var index = new Dictionary<string, (CompProject Project, CompFile File)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (projectId, files) in CompApi.FilesCache)
        {
            if (!CompProject.Cache.TryGetValue(projectId, out var project))
                continue;
            foreach (var file in files.ToList())
                if (!string.IsNullOrEmpty(file.Hash))
                    index.TryAdd(file.Hash, (project, file));
        }

        if (index.Count == 0)
            return;
        foreach (var item in items)
        {
            if (item.IsFolder || string.IsNullOrEmpty(item.ModrinthHash))
                continue;
            if (!index.TryGetValue(item.ModrinthHash, out var hit))
                continue;
            item.Comp = hit.Project;
            item.compFile = hit.File;
        }
    }

    /// <summary>
    ///     实例资源目录中的一个文件或文件夹，对应 WPF 的 LocalCompFile。
    ///     文件夹的路径形如 <c>...\__FOLDER__</c>，实际路径为去掉该后缀后的目录。
    /// </summary>
    public sealed class LocalCompFile
    {
        /// <summary>文件夹项在路径末尾追加的标记。</summary>
        internal const string FolderMarker = "__FOLDER__";

        private static readonly string FolderSuffix = Path.DirectorySeparatorChar + FolderMarker;

        private readonly LocalMod? _mod;
        private readonly bool _unavailable;

        /// <summary>
        ///     文件的完整路径。已禁用的 Mod 带 .disabled 或 .old 后缀。
        /// </summary>
        public string path { get; }

        /// <summary>资源对应的在线工程，未能关联时为 null。</summary>
        public CompProject? Comp { get; internal set; }

        /// <summary>与当前文件内容匹配的在线文件，未能关联时为 null。</summary>
        public CompFile? compFile { get; internal set; }

        /// <summary>检测到的可用更新，没有时为 null。TODO(shion): 更新检测尚未接入 Shion 的网络层。</summary>
        public CompFile? UpdateFile { get; internal set; }

        /// <summary>内嵌（Jar-in-Jar）的 Mod。TODO(shion): 嵌套解析尚未移植，目前恒为空。</summary>
        public List<LocalCompFile>? EmbeddedMods { get; set; }

        /// <summary>内嵌 Mod 的加载器信息。</summary>
        public string? JijLoader { get; internal set; }

        /// <summary>内嵌 Mod 的目标 Minecraft 版本。</summary>
        public string? JijTargetMcVersion { get; internal set; }

        /// <summary>内嵌 Mod 的标识。</summary>
        public string? JijIdentifier { get; internal set; }

        /// <summary>内嵌 Mod 的版本范围。</summary>
        public string? JijVersionRange { get; internal set; }

        /// <summary>内嵌 Mod 的构件版本。</summary>
        public string? JijArtifactVersion { get; internal set; }

        /// <summary>内嵌依赖尚未解析。当前实现下恒为 false。</summary>
        public bool JijPending => false;

        /// <summary>立即解析内嵌依赖。当前实现下恒为成功。</summary>
        public bool ResolveJijNow() => true;

        /// <summary>内容变化（如在线信息更新）时触发。</summary>
        public event Action<LocalCompFile>? OnCompUpdate;

        /// <summary>
        ///     读取文件信息。文件夹与非 Mod 的资源只包含文件名信息，Mod 会通过 <see cref="ModScanner"/> 读取元数据。
        /// </summary>
        public LocalCompFile(string path)
        {
            this.path = path;
            if (IsFolder)
                return;

            if (!IsModFileName(Path.GetFileName(path)))
                return;
            try
            {
                _mod = ModScanner.Read(path);
            }
            catch (Exception ex)
            {
                _unavailable = true;
                FileUnavailableReason = ex;
            }
        }

        /// <summary>
        ///     由 Core 扫描得到的 Mod 信息构造。
        /// </summary>
        public LocalCompFile(LocalMod mod)
        {
            _mod = mod;
            path = mod.FilePath;
        }

        /// <summary>是否为文件夹项。</summary>
        public bool IsFolder => path.EndsWith(FolderSuffix, StringComparison.OrdinalIgnoreCase);

        /// <summary>文件夹的实际路径，文件则与 <see cref="path"/> 相同。</summary>
        public string ActualPath => IsFolder ? path[..^FolderSuffix.Length] : path;

        /// <summary>文件名。文件夹项返回文件夹名。</summary>
        public string FileName => Path.GetFileName(ActualPath);

        /// <summary>去掉 .disabled 与 .old 后缀的文件名。</summary>
        public string RawFileName => FileName.Replace(".disabled", "").Replace(".old", "");

        /// <summary>去掉 .disabled 与 .old 后缀的完整路径，启用与禁用的同一个 Mod 具有相同的 RawPath。</summary>
        public string RawPath
        {
            get
            {
                if (IsFolder)
                    return path;
                var directory = Path.GetDirectoryName(path) ?? "";
                return Path.Combine(directory, RawFileName);
            }
        }

        /// <summary>当前的启用状态。</summary>
        public LocalFileStatus State
        {
            get
            {
                if (IsFolder)
                    return LocalFileStatus.Fine;
                if (_unavailable)
                    return LocalFileStatus.Unavailable;
                var name = FileName.ToLowerInvariant();
                return name.EndsWith(".disabled", StringComparison.Ordinal) || name.EndsWith(".old", StringComparison.Ordinal)
                    ? LocalFileStatus.Disabled
                    : LocalFileStatus.Fine;
            }
        }

        /// <summary>Mod ID，读取不到时为 null。</summary>
        public string? ModId => _mod?.Id;

        /// <summary>读取到的 Mod 元数据；不是 Mod 文件或读取失败时为 null。</summary>
        public LocalMod? Mod => _mod;

        /// <summary>显示名称，读取不到时为 null。</summary>
        public string? Name => _mod?.Name;

        /// <summary>版本号，读取不到时为 null。</summary>
        public string? Version => _mod?.Version;

        /// <summary>文件无法读取时的原因。</summary>
        public Exception? FileUnavailableReason { get; private set; }

        /// <summary>描述。Mod 的元数据来自 Core，目前不含描述；原理图等文件由 <see cref="LoadNbtDataIfNeeded"/> 填充。</summary>
        public string? Description { get; private set; }

        /// <summary>作者。原理图等文件由 <see cref="LoadNbtDataIfNeeded"/> 填充。</summary>
        public string? Authors { get; private set; }

        #region 原理图与结构文件信息（由 LoadNbtDataIfNeeded 填充）

        private bool _nbtLoaded;

        /// <summary>Litematica 原理图的原始名称。</summary>
        public string? LitematicOriginalName { get; private set; }

        /// <summary>Litematica 原理图的格式版本。</summary>
        public int? LitematicVersion { get; private set; }

        /// <summary>包围盒尺寸，格式为「长 × 高 × 宽」。</summary>
        public string? LitematicEnclosingSize { get; private set; }

        /// <summary>区域数量。</summary>
        public int? LitematicRegionCount { get; private set; }

        /// <summary>创建时间（Unix 毫秒）。</summary>
        public long? LitematicTimeCreated { get; private set; }

        /// <summary>修改时间（Unix 毫秒）。</summary>
        public long? LitematicTimeModified { get; private set; }

        /// <summary>总体积。</summary>
        public long? LitematicTotalVolume { get; private set; }

        /// <summary>方块总数。</summary>
        public long? LitematicTotalBlocks { get; private set; }

        /// <summary>Sponge 原理图（.schem）的原始名称。</summary>
        public string? SchemOriginalName { get; private set; }

        /// <summary>Sponge 原理图的格式版本。</summary>
        public int? SpongeVersion { get; private set; }

        /// <summary>结构文件（.nbt）的游戏版本，仅保留字段，当前未解析。</summary>
        public string? StructureGameVersion { get; private set; }

        /// <summary>结构文件的数据版本。</summary>
        public int? StructureDataVersion { get; private set; }

        /// <summary>结构文件的作者。</summary>
        public string? StructureAuthor { get; private set; }

        /// <summary>
        ///     读取原理图或结构文件的 NBT 信息。仅对 .litematic、.schem、.nbt 生效，且只读取一次。
        ///     TODO(shion): .schematic（MCEdit）格式的尺寸与版本信息尚未解析。
        /// </summary>
        public void LoadNbtDataIfNeeded()
        {
            if (_nbtLoaded || IsFolder)
                return;
            _nbtLoaded = true;

            var lower = FileName.ToLowerInvariant();
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var first = stream.ReadByte();
                var second = stream.ReadByte();
                stream.Position = 0;
                var root = first == 0x1f && second == 0x8b
                    ? NbtReader.ReadGZip(stream)
                    : NbtReader.Read(stream);

                if (lower.EndsWith(".litematic", StringComparison.Ordinal))
                {
                    LitematicVersion = ReadInt(root, "Version");
                    var meta = root.GetCompound("Metadata");
                    if (meta is not null)
                    {
                        LitematicOriginalName = meta.GetString("Name");
                        Authors = meta.GetString("Author");
                        Description = meta.GetString("Description");
                        LitematicRegionCount = ReadInt(meta, "RegionCount");
                        LitematicTimeCreated = meta.GetLong("TimeCreated");
                        LitematicTimeModified = meta.GetLong("TimeModified");
                        LitematicTotalVolume = meta.GetLong("TotalVolume");
                        LitematicTotalBlocks = meta.GetLong("TotalBlocks");
                        LitematicEnclosingSize = FormatSize(meta.GetCompound("EnclosingSize"));
                    }
                }
                else if (lower.EndsWith(".schem", StringComparison.Ordinal))
                {
                    SpongeVersion = ReadInt(root, "Version");
                    StructureDataVersion = ReadInt(root, "DataVersion");
                    SchemOriginalName = root.GetString("Name");
                    var width = root.GetLong("Width");
                    var height = root.GetLong("Height");
                    var length = root.GetLong("Length");
                    if (width is not null && height is not null && length is not null)
                        LitematicEnclosingSize = $"{width} × {height} × {length}";
                    if (root.GetCompound("Metadata") is { } spongeMeta)
                    {
                        Authors = spongeMeta.GetString("Author");
                        Description = spongeMeta.GetString("Description");
                    }
                }
                else if (lower.EndsWith(".nbt", StringComparison.Ordinal))
                {
                    StructureDataVersion = ReadInt(root, "DataVersion");
                    StructureAuthor = root.GetString("author");
                }
            }
            catch (Exception ex)
            {
                FileUnavailableReason ??= ex;
            }
        }

        private static int? ReadInt(NbtCompound compound, string name) =>
            compound.GetLong(name) is { } value ? (int)value : null;

        private static string? FormatSize(NbtCompound? size)
        {
            if (size is null)
                return null;
            var x = size.GetLong("x");
            var y = size.GetLong("y");
            var z = size.GetLong("z");
            return x is null || y is null || z is null ? null : $"{x} × {y} × {z}";
        }

        #endregion

        /// <summary>资源的网页地址，来自关联的在线工程。</summary>
        public string? Url => string.IsNullOrEmpty(Comp?.Website) ? null : Comp!.Website;

        /// <summary>检测到的加载器，例如 fabric、forge。</summary>
        public string? DetectedLoader => _mod?.Loaders.FirstOrDefault();

        /// <summary>声明的依赖。</summary>
        public IReadOnlyList<ModDependency> Dependencies => _mod?.Dependencies ?? [];

        /// <summary>该文件额外提供的 Mod ID（provides 别名等）。</summary>
        public IReadOnlyList<string> Provides => _mod?.Provides ?? [];

        /// <summary>用于在线查询的 SHA-1，小写十六进制。</summary>
        public string ModrinthHash => _mod?.Sha1 ?? "";

        /// <summary>标签，供 MyListItem.Tags 使用，即在线工程的标签键。</summary>
        public object? Tags => Comp?.TagKeys;

        /// <summary>是否有可用的更新。</summary>
        public bool CanUpdate => UpdateFile is { Available: true };

        /// <summary>
        ///     复制已加载的在线关联信息，用于重命名后保留状态。
        /// </summary>
        public void CopyLoadedStateFrom(LocalCompFile other)
        {
            Comp = other.Comp;
            compFile = other.compFile;
            UpdateFile = other.UpdateFile;
            EmbeddedMods = other.EmbeddedMods;
            JijLoader = other.JijLoader;
            JijTargetMcVersion = other.JijTargetMcVersion;
            JijIdentifier = other.JijIdentifier;
            JijVersionRange = other.JijVersionRange;
            JijArtifactVersion = other.JijArtifactVersion;
        }

        /// <summary>
        ///     导出可持久化的信息。TODO(shion): 当前仅记录路径，缓存文件尚未接入。
        /// </summary>
        public JsonObject ToJson() => new() { ["path"] = path };

        /// <summary>
        ///     从 <see cref="ToJson"/> 的结果恢复。TODO(shion): 缓存文件尚未接入，当前不做任何事。
        /// </summary>
        public void FromJson(JsonObject json)
        {
        }

        /// <summary>
        ///     判断文件名是否为可作为 Mod 读取的文件（.jar 或 .litemod，可带 .disabled 或 .old 后缀）。
        /// </summary>
        public static bool IsModFileName(string nameOrPath)
        {
            var lower = nameOrPath.ToLowerInvariant();
            return lower.EndsWith(".jar", StringComparison.Ordinal) ||
                   lower.EndsWith(".litemod", StringComparison.Ordinal) ||
                   lower.EndsWith(".jar.disabled", StringComparison.Ordinal) ||
                   lower.EndsWith(".jar.old", StringComparison.Ordinal) ||
                   lower.EndsWith(".litemod.disabled", StringComparison.Ordinal) ||
                   lower.EndsWith(".litemod.old", StringComparison.Ordinal);
        }

        /// <summary>
        ///     判断路径是否为指定资源类型的文件。
        /// </summary>
        public static bool IsCompFile(string path, CompType compType)
        {
            if (compType == CompType.Mod)
                return IsModFileName(Path.GetFileName(path));

            var name = Path.GetFileName(path).ToLowerInvariant();
            string[] extensions = compType switch
            {
                CompType.ResourcePack or CompType.Shader => [".zip"],
                CompType.Schematic => [".litematic", ".nbt", ".schematic", ".schem"],
                _ => []
            };
            return extensions.Any(ext => name.EndsWith(ext, StringComparison.Ordinal));
        }

        public override bool Equals(object? obj) =>
            obj is LocalCompFile other && string.Equals(path, other.path, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(path);

        public override string ToString() => path;

        /// <summary>
        ///     资源的启用状态。
        /// </summary>
        public enum LocalFileStatus
        {
            /// <summary>已启用，或不区分启用状态的资源。</summary>
            Fine,

            /// <summary>已禁用（文件名带 .disabled 或 .old 后缀）。</summary>
            Disabled,

            /// <summary>无法读取。</summary>
            Unavailable
        }
    }
}
