using CommunityToolkit.Mvvm.ComponentModel;

namespace QuickLink.Models;

#pragma warning disable MVVMTK0045
public partial class ResourceUrl : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _label = string.Empty;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    [ObservableProperty]
    private string _addedBy = string.Empty;

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private bool _isDeprecated;
}
#pragma warning restore MVVMTK0045
