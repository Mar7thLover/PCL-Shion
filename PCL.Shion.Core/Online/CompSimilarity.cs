namespace PCL.Shion.Core.Online;

/// <summary>
/// 资源搜索结果的文本相似度排序。
/// </summary>
public static class CompSimilarity
{
    private const double LengthPowerBase = 1.4;
    private const double LengthWeightOffset = 3.6;
    private const double PositionBonusFactor = 0.3;
    private const int MaxPositionBonusDistance = 3;
    private const double SourceLengthImpactFactor = 3.0;
    private const double SourceLengthSmoothing = 15.0;
    private const double ShortQueryBonusFactor = 2.0;

    /// <summary>
    /// 进行多段文本加权搜索。完全匹配的条目排在前面，其余按相似度取前若干个且不低于最低相似度。
    /// </summary>
    public static List<CompSearchEntry<T>> Search<T>(
        List<CompSearchEntry<T>> entries,
        string query,
        int maxBlurCount = 5,
        double minBlurSimilarity = 0.1d)
    {
        var resultList = new List<CompSearchEntry<T>>();
        if (entries.Count == 0)
            return resultList;

        var queryParts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (queryParts.Length == 0)
        {
            resultList.AddRange(entries);
            return resultList;
        }

        var queryPartsLower = queryParts.Select(q => q.ToLowerInvariant()).ToArray();

        foreach (var entry in entries)
        {
            entry.Similarity = SearchSimilarityWeighted(entry.SearchSource, query);

            var processedSources = entry.SearchSource
                .Select(s => s.Aliases.Select(a => a.Replace(" ", "").ToLowerInvariant()).ToArray())
                .ToList();

            var isAbsoluteRight = true;
            foreach (var part in queryPartsLower)
            {
                if (!processedSources.Any(aliases => aliases.Any(alias => alias.Contains(part))))
                {
                    isAbsoluteRight = false;
                    break;
                }
            }

            entry.AbsoluteRight = isAbsoluteRight;
        }

        var sortedEntries = entries
            .OrderByDescending(e => e.AbsoluteRight)
            .ThenByDescending(e => e.Similarity)
            .ToList();

        var blurCount = 0;
        foreach (var entry in sortedEntries)
        {
            if (entry.AbsoluteRight)
            {
                resultList.Add(entry);
            }
            else
            {
                if (entry.Similarity < minBlurSimilarity || blurCount >= maxBlurCount) break;
                resultList.Add(entry);
                blurCount += 1;
            }
        }

        return resultList;
    }

    private static double SearchSimilarityWeighted(List<CompSearchSource> source, string query)
    {
        var totalWeight = 0d;
        var sum = 0d;
        foreach (var pair in source)
        {
            if (pair.Aliases.Length > 0)
                sum += pair.Aliases.Max(a => SearchSimilarity(a, query)) * pair.Weight;
            totalWeight += pair.Weight;
        }

        return sum / totalWeight;
    }

    private static double SearchSimilarity(string source, string query)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(query))
            return 0.0;

        var sourceText = source.ToLowerInvariant().Replace(" ", "");
        var queryText = query.ToLowerInvariant().Replace(" ", "");
        var sourceSpan = sourceText.AsSpan();
        var querySpan = queryText.AsSpan();

        if (sourceSpan.IsEmpty || querySpan.IsEmpty)
            return 0.0;

        var usedSourceIndices = new bool[sourceSpan.Length];
        double weightedLengthSum = 0;
        var queryIndex = 0;

        while (queryIndex < querySpan.Length)
        {
            var longestMatchLength = 0;
            var bestMatchSourceStartIndex = -1;

            for (var sourceIndex = 0; sourceIndex < sourceSpan.Length; sourceIndex++)
            {
                var currentMatchLength = 0;
                while (queryIndex + currentMatchLength < querySpan.Length &&
                       sourceIndex + currentMatchLength < sourceSpan.Length &&
                       !usedSourceIndices[sourceIndex + currentMatchLength] &&
                       sourceSpan[sourceIndex + currentMatchLength] == querySpan[queryIndex + currentMatchLength])
                    currentMatchLength++;

                if (currentMatchLength <= longestMatchLength)
                    continue;

                longestMatchLength = currentMatchLength;
                bestMatchSourceStartIndex = sourceIndex;
            }

            if (longestMatchLength > 0)
            {
                for (var i = 0; i < longestMatchLength; i++)
                    usedSourceIndices[bestMatchSourceStartIndex + i] = true;

                var incrementWeight = Math.Pow(LengthPowerBase, 3 + longestMatchLength) - LengthWeightOffset;
                var positionDifference = Math.Abs(queryIndex - bestMatchSourceStartIndex);
                var positionBonus = 1.0 + PositionBonusFactor * Math.Max(0, MaxPositionBonusDistance - positionDifference);
                incrementWeight *= positionBonus;

                weightedLengthSum += incrementWeight;
            }

            queryIndex += Math.Max(1, longestMatchLength);
        }

        var normalizedScore = weightedLengthSum / querySpan.Length;
        var sourceLengthPenalty = SourceLengthImpactFactor / Math.Sqrt(sourceSpan.Length + SourceLengthSmoothing);
        var shortQueryBonus = query.Length == 1 ? ShortQueryBonusFactor : 1.0;

        return normalizedScore * sourceLengthPenalty * shortQueryBonus;
    }
}

/// <summary>
/// 搜索条目的一个文本来源，包含若干别名与权重。
/// </summary>
/// <param name="Aliases">文本别名，取最相似的一个计分。</param>
/// <param name="Weight">权重。</param>
public sealed record CompSearchSource(string[] Aliases, double Weight = 1d)
{
    public CompSearchSource(string text, double weight = 1d) : this([text], weight)
    {
    }
}

/// <summary>
/// 参与相似度搜索的条目。
/// </summary>
/// <typeparam name="T">条目对应的源数据类型。</typeparam>
public sealed class CompSearchEntry<T>(T item, List<CompSearchSource> searchSource)
{
    /// <summary>该条目对应的源数据。</summary>
    public T Item { get; } = item;

    /// <summary>该条目用于搜索的文本来源。</summary>
    public List<CompSearchSource> SearchSource { get; } = searchSource;

    /// <summary>计算出的相似度。</summary>
    public double Similarity { get; set; }

    /// <summary>是否完全匹配。</summary>
    public bool AbsoluteRight { get; set; }
}
