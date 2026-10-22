namespace PCL.Shion.Core.Minecraft;

/// <summary>
/// 读取版本 JSON 并沿 inheritsFrom 链合并成完整的版本信息。
/// </summary>
public static class VersionResolver
{
    /// <exception cref="IOException">版本 JSON（包括其父版本）不存在或无法读取。</exception>
    /// <exception cref="InvalidDataException">版本 JSON 内容无效，或 inheritsFrom 成环。</exception>
    public static ResolvedVersion Resolve(string minecraftRoot, string versionName)
    {
        var versionsDirectory = Path.Combine(minecraftRoot, "versions");
        var chain = LoadChain(versionsDirectory, versionName);

        string? mainClass = null;
        string? type = null;
        string? assetIndexId = null;
        FileDownload? assetIndexDownload = null;
        int? javaMajorVersion = null;
        string? javaComponent = null;
        string? legacyArguments = null;
        string? loggingArgument = null;
        string? loggingFileId = null;
        FileDownload? loggingFileDownload = null;
        var libraries = new List<VersionLibrary>();
        var jvmArguments = new List<VersionArgument>();
        var gameArguments = new List<VersionArgument>();

        // 从继承链根部向子版本合并：标量字段由子版本覆盖，列表按先父后子拼接
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var file = chain[i].File;
            mainClass = file.MainClass ?? mainClass;
            type = file.Type ?? type;
            if (file.AssetIndexId is not null)
            {
                // 子版本换了索引却没给地址时，父版本的地址已不适用
                assetIndexDownload = file.AssetIndexDownload ?? (file.AssetIndexId == assetIndexId ? assetIndexDownload : null);
                assetIndexId = file.AssetIndexId;
            }
            javaMajorVersion = file.JavaMajorVersion ?? javaMajorVersion;
            javaComponent = file.JavaComponent ?? javaComponent;
            legacyArguments = file.MinecraftArguments ?? legacyArguments;
            if (file.LoggingFileId is not null)
            {
                loggingArgument = file.LoggingArgument;
                loggingFileId = file.LoggingFileId;
                loggingFileDownload = file.LoggingFileDownload;
            }
            libraries.AddRange(file.Libraries);
            jvmArguments.AddRange(file.JvmArguments);
            gameArguments.AddRange(file.GameArguments);
        }

        var (clientJarPath, clientJarDownload) = FindClientJar(versionsDirectory, chain);
        return new ResolvedVersion
        {
            Id = versionName,
            MainClass = mainClass ?? throw new InvalidDataException($"Version '{versionName}' does not define a main class."),
            Type = type,
            AssetIndexId = assetIndexId ?? "legacy",
            AssetIndexDownload = assetIndexDownload,
            JavaMajorVersion = javaMajorVersion,
            JavaComponent = javaComponent,
            ClientJarPath = clientJarPath,
            ClientJarDownload = clientJarDownload,
            MinecraftVersion = DetectMinecraftVersion(chain),
            Loader = LoaderDetector.Detect(libraries.Select(library => library.Name)),
            Libraries = libraries,
            JvmArguments = jvmArguments,
            GameArguments = gameArguments,
            LegacyMinecraftArguments = legacyArguments,
            LoggingArgument = loggingArgument,
            LoggingFileId = loggingFileId,
            LoggingFileDownload = loggingFileDownload
        };
    }

    /// <summary>
    /// 依次读取版本及其祖先的 JSON，子版本在前。
    /// </summary>
    private static List<(string Name, VersionFile File)> LoadChain(string versionsDirectory, string versionName)
    {
        var chain = new List<(string Name, VersionFile File)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? name = versionName;
        while (name is not null)
        {
            if (!visited.Add(name))
                throw new InvalidDataException($"Circular inheritsFrom chain involving '{name}'.");

            var file = VersionFile.Load(Path.Combine(versionsDirectory, name, $"{name}.json"));
            chain.Add((name, file));
            name = file.InheritsFrom;
        }
        return chain;
    }

    /// <summary>
    /// 客户端 jar 取继承链上最靠近子版本且文件存在的那个；都不存在时，取最先声明了客户端下载的版本，
    /// 仍没有则取根版本，这样缺失文件会指向本应提供它的版本。
    /// 下载信息只取 jar 所属版本自己声明的，以免按父版本的校验值覆盖子版本改过的 jar。
    /// </summary>
    private static (string Path, FileDownload? Download) FindClientJar(
        string versionsDirectory,
        List<(string Name, VersionFile File)> chain)
    {
        foreach (var (name, file) in chain)
        {
            var path = GetJarPath(versionsDirectory, name);
            if (File.Exists(path))
                return (path, file.ClientJarDownload);
        }

        foreach (var (name, file) in chain)
        {
            if (file.ClientJarDownload is not null)
                return (GetJarPath(versionsDirectory, name), file.ClientJarDownload);
        }
        return (GetJarPath(versionsDirectory, chain[^1].Name), null);
    }

    private static string GetJarPath(string versionsDirectory, string name)
    {
        return Path.Combine(versionsDirectory, name, $"{name}.jar");
    }

    /// <summary>
    /// 有继承时取继承链根部的版本名；否则依次取 clientVersion、版本 ID 开头的原版版本号。
    /// </summary>
    private static string? DetectMinecraftVersion(List<(string Name, VersionFile File)> chain)
    {
        if (chain.Count > 1)
            return chain[^1].Name;

        var (name, file) = chain[0];
        return file.ClientVersion ?? VanillaVersionName.TryExtract(name);
    }
}
