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
        int? javaMajorVersion = null;
        string? legacyArguments = null;
        string? loggingArgument = null;
        string? loggingFileId = null;
        var libraries = new List<VersionLibrary>();
        var jvmArguments = new List<VersionArgument>();
        var gameArguments = new List<VersionArgument>();

        // 从继承链根部向子版本合并：标量字段由子版本覆盖，列表按先父后子拼接
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var file = chain[i].File;
            mainClass = file.MainClass ?? mainClass;
            type = file.Type ?? type;
            assetIndexId = file.AssetIndexId ?? assetIndexId;
            javaMajorVersion = file.JavaMajorVersion ?? javaMajorVersion;
            legacyArguments = file.MinecraftArguments ?? legacyArguments;
            if (file.LoggingFileId is not null)
            {
                loggingArgument = file.LoggingArgument;
                loggingFileId = file.LoggingFileId;
            }
            libraries.AddRange(file.Libraries);
            jvmArguments.AddRange(file.JvmArguments);
            gameArguments.AddRange(file.GameArguments);
        }

        return new ResolvedVersion
        {
            Id = versionName,
            MainClass = mainClass ?? throw new InvalidDataException($"Version '{versionName}' does not define a main class."),
            Type = type,
            AssetIndexId = assetIndexId ?? "legacy",
            JavaMajorVersion = javaMajorVersion,
            ClientJarPath = FindClientJar(versionsDirectory, chain),
            MinecraftVersion = DetectMinecraftVersion(chain),
            Loader = LoaderDetector.Detect(libraries.Select(library => library.Name)),
            Libraries = libraries,
            JvmArguments = jvmArguments,
            GameArguments = gameArguments,
            LegacyMinecraftArguments = legacyArguments,
            LoggingArgument = loggingArgument,
            LoggingFileId = loggingFileId
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
    /// </summary>
    private static string FindClientJar(string versionsDirectory, List<(string Name, VersionFile File)> chain)
    {
        foreach (var (name, _) in chain)
        {
            var path = GetJarPath(versionsDirectory, name);
            if (File.Exists(path))
                return path;
        }

        foreach (var (name, file) in chain)
        {
            if (file.DeclaresClientJar)
                return GetJarPath(versionsDirectory, name);
        }
        return GetJarPath(versionsDirectory, chain[^1].Name);
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
