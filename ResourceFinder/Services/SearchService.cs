using ResourceFinder.Models;

namespace ResourceFinder.Services;

public class SearchService(IResourceRepository repo)
{
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query)
    {
        var all = await repo.GetAllAsync();

        if (string.IsNullOrWhiteSpace(query))
        {
            return all
                .OrderBy(r => r.IsDeprecated)
                .ThenBy(r => r.Name)
                .Select(r => ToResult(r, 1.0))
                .Take(50)
                .ToList();
        }

        var q = query.Trim().ToLowerInvariant();
        return all
            .Select(r => (resource: r, score: ScoreResource(r, q)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.resource.IsDeprecated)
            .Take(50)
            .Select(x => ToResult(x.resource, x.score))
            .ToList();
    }

    private static SearchResult ToResult(Resource r, double score) => new()
    {
        Resource = r,
        Score = score,
        CurrentUrl = r.CurrentUrl?.Url ?? r.Urls.FirstOrDefault(u => !u.IsDeprecated)?.Url ?? string.Empty
    };

    private static double ScoreResource(Resource r, string query)
    {
        double best = 0;
        best = Math.Max(best, FuzzyScore(r.Name.ToLowerInvariant(), query));
        best = Math.Max(best, FuzzyScore(r.Description.ToLowerInvariant(), query) * 0.85);
        foreach (var tag in r.Tags)
            best = Math.Max(best, FuzzyScore(tag.ToLowerInvariant(), query) * 0.9);
        if (r.CurrentUrl != null)
            best = Math.Max(best, FuzzyScore(r.CurrentUrl.Url.ToLowerInvariant(), query) * 0.7);
        return best;
    }

    private static double FuzzyScore(string text, string query)
    {
        if (text == query) return 1.0;
        if (text.StartsWith(query)) return 0.9;
        if (text.Contains(query)) return 0.75;
        if (IsSubsequence(query, text)) return 0.5;
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
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = a[i - 1] == b[j - 1]
                    ? d[i - 1, j - 1]
                    : 1 + Math.Min(d[i - 1, j - 1], Math.Min(d[i - 1, j], d[i, j - 1]));
        return d[a.Length, b.Length];
    }
}
