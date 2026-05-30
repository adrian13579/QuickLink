using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace QuickLink.Models;

#pragma warning disable MVVMTK0045
public partial class Resource : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private List<string> _tags = [];

    [ObservableProperty]
    private bool _isDeprecated;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentUrl))]
    private Guid? _currentUrlId;

    public ObservableCollection<ResourceUrl> Urls { get; set; } = [];

    [JsonIgnore]
    public ResourceUrl? CurrentUrl => CurrentUrlId.HasValue
        ? Urls.FirstOrDefault(u => u.Id == CurrentUrlId.Value)
        : null;
}
#pragma warning restore MVVMTK0045
