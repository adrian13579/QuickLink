using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ResourceFinder.Models;
using ResourceFinder.Services;

namespace ResourceFinder.ViewModels;

#pragma warning disable MVVMTK0045
public partial class SearchViewModel : ObservableObject
{
    private readonly SearchService _search;
    private readonly SettingsService _settings;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<SearchResult> Results { get; } = [];

    public SearchViewModel(SearchService search, SettingsService settings)
    {
        _search = search;
        _settings = settings;
    }

    partial void OnSearchTextChanged(string value) => _ = LoadResultsAsync(value);

    public bool IsEmpty => Results.Count == 0;

    public async Task LoadResultsAsync(string query)
    {
        var results = await _search.SearchAsync(query);
        Results.Clear();
        foreach (var r in results)
            Results.Add(r);
        OnPropertyChanged(nameof(IsEmpty));
    }

    public bool OpenInBrowser => _settings.Current.DefaultAction == "OpenInBrowser";
}
#pragma warning restore MVVMTK0045
