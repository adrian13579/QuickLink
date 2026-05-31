namespace QuickLink.Models;

public class SearchResult
{
    public Resource Resource { get; set; } = null!;
    public double Score { get; set; }
    public string CurrentUrl { get; set; } = string.Empty;
    public List<HighlightRange> MatchRanges { get; set; } = [];
    public List<TagResult> TagResults { get; set; } = [];
}
