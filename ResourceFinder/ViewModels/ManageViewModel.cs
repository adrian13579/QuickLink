using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickLink.Models;
using QuickLink.Services;

namespace QuickLink.ViewModels;

#pragma warning disable MVVMTK0045
public partial class ManageViewModel : ObservableObject
{
    private readonly IResourceRepository _repo;

    public ObservableCollection<Resource> Resources { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(EditName))]
    [NotifyPropertyChangedFor(nameof(EditDescription))]
    [NotifyPropertyChangedFor(nameof(EditTags))]
    [NotifyPropertyChangedFor(nameof(EditIsDeprecated))]
    private Resource? _selectedResource;

    [ObservableProperty]
    private string _filterText = string.Empty;

    public bool HasSelection => SelectedResource != null;

    public bool IsDirty { get; set; }

    private (string Name, string Description, List<string> Tags, bool IsDeprecated)? _snapshot;

    partial void OnSelectedResourceChanged(Resource? value)
    {
        IsDirty = false;
        _snapshot = value is null
            ? null
            : (value.Name, value.Description, [.. value.Tags], value.IsDeprecated);
    }

    public void DiscardChanges()
    {
        IsDirty = false;
        if (SelectedResource is null || _snapshot is null) return;
        SelectedResource.Name        = _snapshot.Value.Name;
        SelectedResource.Description = _snapshot.Value.Description;
        SelectedResource.Tags        = _snapshot.Value.Tags;
        SelectedResource.IsDeprecated = _snapshot.Value.IsDeprecated;
        OnPropertyChanged(nameof(EditName));
        OnPropertyChanged(nameof(EditDescription));
        OnPropertyChanged(nameof(EditTags));
        OnPropertyChanged(nameof(EditIsDeprecated));
    }

    public string EditName
    {
        get => SelectedResource?.Name ?? string.Empty;
        set
        {
            if (SelectedResource != null && SelectedResource.Name != value)
            {
                SelectedResource.Name = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }
    }

    public string EditDescription
    {
        get => SelectedResource?.Description ?? string.Empty;
        set
        {
            if (SelectedResource != null && SelectedResource.Description != value)
            {
                SelectedResource.Description = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }
    }

    public string EditTags
    {
        get => SelectedResource != null ? string.Join(", ", SelectedResource.Tags) : string.Empty;
        set
        {
            if (SelectedResource == null) return;
            var parsed = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (SelectedResource.Tags.SequenceEqual(parsed)) return;
            SelectedResource.Tags = [.. parsed];
            IsDirty = true;
            OnPropertyChanged();
        }
    }

    public bool EditIsDeprecated
    {
        get => SelectedResource?.IsDeprecated ?? false;
        set
        {
            if (SelectedResource != null && SelectedResource.IsDeprecated != value)
            {
                SelectedResource.IsDeprecated = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }
    }

    public ManageViewModel(IResourceRepository repo) => _repo = repo;

    public async Task LoadSingleAsync(Guid id)
    {
        var resource = await _repo.GetByIdAsync(id);
        SelectedResource = resource;
    }

    public async Task LoadAsync()
    {
        var all = await _repo.GetAllAsync();
        Resources.Clear();
        foreach (var r in all.OrderBy(r => r.Name))
            Resources.Add(r);
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (SelectedResource == null) return;
        await _repo.SaveAsync(SelectedResource);
        IsDirty = false;
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (SelectedResource == null) return;
        var toRemove = SelectedResource;
        SelectedResource = null;
        await _repo.DeleteAsync(toRemove.Id);
        Resources.Remove(toRemove);
    }

    [RelayCommand]
    public async Task AddResourceAsync()
    {
        var r = new Resource { Name = "New Resource" };
        await _repo.SaveAsync(r);
        Resources.Add(r);
        SelectedResource = r;
    }

    [RelayCommand]
    public async Task AddUrlAsync(string url)
    {
        if (SelectedResource == null || string.IsNullOrWhiteSpace(url)) return;
        var entry = new ResourceUrl { Url = url, AddedAt = DateTime.UtcNow, IsCurrent = false };
        SelectedResource.Urls.Add(entry); // ObservableCollection notifies ItemsControl
        await _repo.SaveAsync(SelectedResource);
    }

    [RelayCommand]
    public async Task SetCurrentUrlAsync(ResourceUrl urlEntry)
    {
        if (SelectedResource == null) return;
        foreach (var u in SelectedResource.Urls) u.IsCurrent = false;
        urlEntry.IsCurrent = true;
        SelectedResource.CurrentUrlId = urlEntry.Id;
        await _repo.SaveAsync(SelectedResource);
        // ResourceUrl and Resource implement INPC, DataTemplate bindings auto-update
    }

    [RelayCommand]
    public async Task DeprecateUrlAsync(ResourceUrl urlEntry)
    {
        if (SelectedResource == null) return;
        urlEntry.IsDeprecated = !urlEntry.IsDeprecated;
        if (urlEntry.IsDeprecated && urlEntry.IsCurrent)
        {
            urlEntry.IsCurrent = false;
            SelectedResource.CurrentUrlId = null;
        }
        await _repo.SaveAsync(SelectedResource);
    }

    [RelayCommand]
    public async Task DeleteUrlAsync(ResourceUrl urlEntry)
    {
        if (SelectedResource == null) return;
        if (urlEntry.IsCurrent)
            SelectedResource.CurrentUrlId = null;
        SelectedResource.Urls.Remove(urlEntry);
        await _repo.SaveAsync(SelectedResource);
    }
}
#pragma warning restore MVVMTK0045
