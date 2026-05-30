using System.Collections.ObjectModel;
using System.Text.Json;
using QuickLink.Models;

namespace QuickLink.Services;

public class JsonResourceRepository : IResourceRepository
{
    private readonly SettingsService _settings;
    private string _filePath;
    private static readonly JsonSerializerOptions _readOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions _writeOptions = new();
    private List<Resource> _cache = [];
    private ReadOnlyCollection<Resource>? _readOnly;
    private bool _loaded;
    private long _version;

    public long Version => _version;

    public JsonResourceRepository(SettingsService settings)
    {
        _settings = settings;
        _filePath = settings.Current.DataFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        settings.Changed += OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        var newPath = _settings.Current.DataFilePath;
        if (_filePath == newPath) return;
        _filePath = newPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        InvalidateCache();
    }

    public async Task<IReadOnlyList<Resource>> GetAllAsync()
    {
        await EnsureLoadedAsync();
        return _readOnly!;
    }

    public async Task<Resource?> GetByIdAsync(Guid id)
    {
        await EnsureLoadedAsync();
        return _cache.FirstOrDefault(r => r.Id == id);
    }

    public async Task SaveAsync(Resource resource)
    {
        await EnsureLoadedAsync();
        var idx = _cache.FindIndex(r => r.Id == resource.Id);
        if (idx >= 0) _cache[idx] = resource;
        else _cache.Add(resource);
        await PersistAsync();
    }

    public async Task SaveRangeAsync(IEnumerable<Resource> resources)
    {
        await EnsureLoadedAsync();
        foreach (var resource in resources)
        {
            var idx = _cache.FindIndex(r => r.Id == resource.Id);
            if (idx >= 0) _cache[idx] = resource;
            else _cache.Add(resource);
        }
        await PersistAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await EnsureLoadedAsync();
        _cache.RemoveAll(r => r.Id == id);
        await PersistAsync();
    }

    public void InvalidateCache()
    {
        _loaded = false;
        _readOnly = null;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        if (File.Exists(_filePath))
        {
            await using var stream = File.OpenRead(_filePath);
            _cache = await JsonSerializer.DeserializeAsync<List<Resource>>(stream, _readOptions) ?? [];
        }
        _readOnly = _cache.AsReadOnly();
        _version++;
        _loaded = true;
    }

    private async Task PersistAsync()
    {
        var tmp = _filePath + ".tmp";
        await using (var stream = File.Create(tmp))
            await JsonSerializer.SerializeAsync(stream, _cache, _writeOptions);
        File.Move(tmp, _filePath, overwrite: true);
        _readOnly = _cache.AsReadOnly();
        _version++;
    }
}
