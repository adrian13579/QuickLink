using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QuickLink.Models;
using QuickLink.Services;

namespace QuickLink.ViewModels;

#pragma warning disable MVVMTK0045
public partial class SearchViewModel : ObservableObject
{
    private readonly SearchService _search;
    private readonly SettingsService _settings;
    private CancellationTokenSource? _cts;
    private bool _searchPending;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<SearchResult> Results { get; } = [];
    public ObservableCollection<SearchResult> PinnedResources { get; } = [];

    public SearchViewModel(SearchService search, SettingsService settings)
    {
        _search = search;
        _settings = settings;
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasQuery));
        OnPropertyChanged(nameof(ShowIdle));
        OnPropertyChanged(nameof(ShowPinned));
        OnPropertyChanged(nameof(ShowIdleEmpty));
        if (!string.IsNullOrWhiteSpace(value))
        {
            _searchPending = true;
            OnPropertyChanged(nameof(ShowNoResults));
        }
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _ = LoadResultsAsync(value, _cts.Token);
    }

    public bool HasQuery => !string.IsNullOrWhiteSpace(SearchText);
    public bool ShowIdle => !HasQuery;
    public bool IsEmpty => Results.Count == 0;
    public bool ShowNoResults => HasQuery && IsEmpty && !_searchPending;
    public bool HasResults => HasQuery && !IsEmpty;
    public int ResultCount => Results.Count;
    public bool HasPinned => PinnedResources.Count > 0;
    public bool ShowPinned => ShowIdle && HasPinned;
    public bool ShowIdleEmpty => ShowIdle && !HasPinned;

    public async Task LoadResultsAsync(string query, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _searchPending = false;
                Results.Clear();
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(ShowNoResults));
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(ResultCount));
                await LoadPinnedAsync();
                return;
            }
            await Task.Delay(150, ct);
            var results = await _search.SearchAsync(query);
            ct.ThrowIfCancellationRequested();
            _searchPending = false;
            Results.Clear();
            foreach (var r in results)
                Results.Add(r);
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ShowNoResults));
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ResultCount));
        }
        catch (OperationCanceledException) { }
    }

    public async Task LoadPinnedAsync()
    {
        var pinned = await _search.GetPinnedAsync();
        PinnedResources.Clear();
        foreach (var p in pinned)
            PinnedResources.Add(p);
        OnPropertyChanged(nameof(HasPinned));
        OnPropertyChanged(nameof(ShowPinned));
        OnPropertyChanged(nameof(ShowIdleEmpty));
    }

    public bool OpenInBrowser => _settings.Current.DefaultAction == "OpenInBrowser";
}
#pragma warning restore MVVMTK0045
