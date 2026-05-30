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

    public SearchViewModel(SearchService search, SettingsService settings)
    {
        _search = search;
        _settings = settings;
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasQuery));
        OnPropertyChanged(nameof(ShowIdle));
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
        }
        catch (OperationCanceledException) { }
    }

    public bool OpenInBrowser => _settings.Current.DefaultAction == "OpenInBrowser";
}
#pragma warning restore MVVMTK0045
