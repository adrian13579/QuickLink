using System.Text.Json;
using ResourceFinder.Models;

namespace ResourceFinder.Services;

public class SettingsService
{
    private static readonly string _folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ResourceFinder");
    private static readonly string _settingsPath = Path.Combine(_folder, "settings.json");
    private static readonly string _defaultDataPath = Path.Combine(_folder, "resources.json");

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public AppSettings Current { get; private set; } = new();

    public SettingsService()
    {
        Directory.CreateDirectory(_folder);
        Load();
        if (string.IsNullOrEmpty(Current.DataFilePath))
            Current.DataFilePath = _defaultDataPath;
    }

    private void Load()
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), _json);
            if (loaded != null) Current = loaded;
        }
        catch { }
    }

    public void Save() =>
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(Current, _json));
}
