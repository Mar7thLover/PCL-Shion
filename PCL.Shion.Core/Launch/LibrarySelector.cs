using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Launch;

/// <summary>
/// 按当前环境筛选版本的库。
/// </summary>
internal static class LibrarySelector
{
    /// <summary>
    /// 先按规则筛选，再按 group:artifact[:classifier] 去重：同一个键后出现的覆盖先出现的，位置保持在首次出现处。
    /// 带 natives 表的旧式库只提供对应当前系统的 natives，自身不进入 classpath。
    /// </summary>
    public static IReadOnlyList<SelectedLibrary> Select(IEnumerable<VersionLibrary> libraries, RuleEnvironment environment)
    {
        var selected = new List<SelectedLibrary>();
        var indexByKey = new Dictionary<string, int>();

        foreach (var library in libraries)
        {
            if (!RuleEvaluator.IsAllowed(library.Rules, environment))
                continue;

            string key;
            SelectedLibrary entry;
            if (library.Natives.Count == 0)
            {
                key = MavenName.GetKey(library.Name);
                entry = new SelectedLibrary(library.ArtifactPath ?? MavenName.GetPath(library.Name), null);
            }
            else if (library.Natives.TryGetValue(environment.OsName, out var template))
            {
                var classifier = template.Replace("${arch}", environment.Arch == "x86" ? "32" : "64");
                var path = library.ClassifierPaths.TryGetValue(classifier, out var classifierPath)
                    ? classifierPath
                    : MavenName.GetPath(library.Name, classifier);
                key = $"{MavenName.GetKey(library.Name)}:{classifier}";
                entry = new SelectedLibrary(path, library.ExtractExclude);
            }
            else
            {
                continue;
            }

            if (indexByKey.TryGetValue(key, out var index))
            {
                selected[index] = entry;
            }
            else
            {
                indexByKey[key] = selected.Count;
                selected.Add(entry);
            }
        }
        return selected;
    }
}
