using System.Text.Json;
using ResourceFinder.Models;

namespace ResourceFinder.Services;

public class JsonResourceRepository : IResourceRepository
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private List<Resource> _cache = [];
    private bool _loaded;

    public JsonResourceRepository(SettingsService settings)
    {
        _filePath = settings.Current.DataFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    public async Task<IReadOnlyList<Resource>> GetAllAsync()
    {
        await EnsureLoadedAsync();
        return _cache.AsReadOnly();
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

    public async Task DeleteAsync(Guid id)
    {
        await EnsureLoadedAsync();
        _cache.RemoveAll(r => r.Id == id);
        await PersistAsync();
    }

    public void InvalidateCache() => _loaded = false;

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        if (File.Exists(_filePath))
        {
            await using var stream = File.OpenRead(_filePath);
            _cache = await JsonSerializer.DeserializeAsync<List<Resource>>(stream, _json) ?? [];
        }
        _loaded = true;
    }

    private async Task PersistAsync()
    {
        var tmp = _filePath + ".tmp";
        await using (var stream = File.Create(tmp))
            await JsonSerializer.SerializeAsync(stream, _cache, _json);
        File.Move(tmp, _filePath, overwrite: true);
    }
}
