using System.Buffers;
using QuickLink.Models;

namespace QuickLink.Services;

public class SearchService(IResourceRepository repo)
{
    private sealed record SearchEntry(
        Resource Resource,
        string NameLower,
        string[] TagsLower);

    private SearchEntry[] _index = [];
    private long _indexVersion = -1;

    public async Task TogglePinAsync(Resource resource)
    {
        resource.IsPinned = !resource.IsPinned;
        await repo.SaveAsync(resource);
    }

    public async Task<IReadOnlyList<SearchResult>> GetPinnedAsync()
    {
        var index = await GetIndexAsync();
        return index
            .Where(e => e.Resource.IsPinned)
            .OrderBy(e => e.Resource.Name)
            .Select(e => ToResult(e, 1.0))
            .ToList();
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query)
    {
        var index = await GetIndexAsync();

        if (string.IsNullOrWhiteSpace(query))
        {
            return index
                .OrderBy(e => e.Resource.IsDeprecated)
                .ThenBy(e => e.Resource.Name)
                .Select(e => ToResult(e, 1.0))
                .Take(50)
                .ToList();
        }

        var q = query.Trim().ToLowerInvariant();
        return index
            .Select(e => (entry: e, score: ScoreEntry(e, q)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.entry.Resource.IsDeprecated)
            .Take(50)
            .Select(x => ToResult(x.entry, x.score, q))
            .ToList();
    }

    private async Task<SearchEntry[]> GetIndexAsync()
    {
        var version = repo.Version;
        if (version == _indexVersion) return _index;

        var all = await repo.GetAllAsync();
        _index = [.. all.Select(BuildEntry)];
        _indexVersion = version;
        return _index;
    }

    private static SearchEntry BuildEntry(Resource r) => new(
        r,
        r.Name.ToLowerInvariant(),
        [.. r.Tags.Select(t => t.ToLowerInvariant())]);

    private static SearchResult ToResult(SearchEntry e, double score, string? query = null) => new()
    {
        Resource = e.Resource,
        Score = score,
        CurrentUrl = e.Resource.CurrentUrl?.Url
            ?? e.Resource.Urls.FirstOrDefault(u => !u.IsDeprecated)?.Url
            ?? string.Empty,
        MatchRanges = string.IsNullOrEmpty(query) ? [] : GetNameRanges(e.Resource.Name, query),
        TagResults = BuildTagResults(e, query)
    };

    private static List<TagResult> BuildTagResults(SearchEntry e, string? query) =>
        [.. e.TagsLower.Select((tl, i) => new TagResult
        {
            Text = e.Resource.Tags[i],
            IsMatched = string.IsNullOrEmpty(query) || FuzzyScore(tl, query) > 0
        })];

    private static List<HighlightRange> GetNameRanges(string name, string query)
    {
        var lower = name.ToLowerInvariant();
        var idx = lower.IndexOf(query, StringComparison.Ordinal);
        if (idx >= 0)
            return [new HighlightRange { StartIndex = idx, Length = query.Length }];
        // Fall back to subsequence positions
        var result = new List<HighlightRange>();
        int qi = 0;
        for (int i = 0; i < lower.Length && qi < query.Length; i++)
        {
            if (lower[i] == query[qi]) { result.Add(new HighlightRange { StartIndex = i, Length = 1 }); qi++; }
        }
        return qi == query.Length ? result : [];
    }

    private static double ScoreEntry(SearchEntry e, string query)
    {
        double best = FuzzyScore(e.NameLower, query);
        foreach (var tag in e.TagsLower)
        {
            if (best >= 1.0) break;
            best = Math.Max(best, FuzzyScore(tag, query) * 0.9);
        }
        return best;
    }

    private static double FuzzyScore(string text, string query)
    {
        if (text == query) return 1.0;
        if (text.StartsWith(query)) return 0.9;
        if (text.Contains(query)) return 0.75;
        if (IsSubsequence(query, text)) return 0.5;
        // Skip Levenshtein when the length ratio makes a good score impossible
        if (text.Length > query.Length * 3) return 0;
        var ratio = 1.0 - (double)LevenshteinDistance(text, query) / Math.Max(text.Length, query.Length);
        return ratio > 0.4 ? ratio * 0.4 : 0;
    }

    private static bool IsSubsequence(string query, string text)
    {
        int qi = 0;
        foreach (var c in text)
            if (qi < query.Length && c == query[qi]) qi++;
        return qi == query.Length;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var pool = ArrayPool<int>.Shared;
        var prev = pool.Rent(b.Length + 1);
        var curr = pool.Rent(b.Length + 1);
        try
        {
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= b.Length; j++)
                    curr[j] = a[i - 1] == b[j - 1]
                        ? prev[j - 1]
                        : 1 + Math.Min(prev[j - 1], Math.Min(prev[j], curr[j - 1]));
                (prev, curr) = (curr, prev);
            }
            return prev[b.Length];
        }
        finally
        {
            pool.Return(prev);
            pool.Return(curr);
        }
    }
}
