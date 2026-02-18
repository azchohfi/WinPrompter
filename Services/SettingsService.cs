using System.Text.Json;

namespace WinPrompter.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    private Dictionary<string, object> _cache;

    public SettingsService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinPrompter");
        Directory.CreateDirectory(appData);
        _settingsPath = Path.Combine(appData, "settings.json");
        _cache = Load();
    }

    public double FontSize
    {
        get => GetValue<double>("FontSize", 36.0);
        set { SetValue("FontSize", value); Save(); }
    }

    public double Speed
    {
        get => GetValue<double>("Speed", 1.0);
        set { SetValue("Speed", value); Save(); }
    }

    public string Theme
    {
        get => GetValue<string>("Theme", "classic");
        set { SetValue("Theme", value); Save(); }
    }

    public bool MirrorMode
    {
        get => GetValue<bool>("MirrorMode", false);
        set { SetValue("MirrorMode", value); Save(); }
    }

    public double Opacity
    {
        get => GetValue<double>("Opacity", 1.0);
        set { SetValue("Opacity", value); Save(); }
    }

    public bool CountdownEnabled
    {
        get => GetValue<bool>("CountdownEnabled", true);
        set { SetValue("CountdownEnabled", value); Save(); }
    }

    public int VoiceSensitivity
    {
        get => (int)GetValue<double>("VoiceSensitivity", 3);
        set { SetValue("VoiceSensitivity", value); Save(); }
    }

    public string RecentFilesJson
    {
        get => GetValue<string>("RecentFiles", "[]");
        set { SetValue("RecentFiles", value); Save(); }
    }

    public List<string> GetRecentFiles()
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(RecentFilesJson) ?? [];
        }
        catch { return []; }
    }

    public void AddRecentFile(string path)
    {
        var files = GetRecentFiles();
        files.Remove(path);
        files.Insert(0, path);
        if (files.Count > 10) files.RemoveRange(10, files.Count - 10);
        RecentFilesJson = System.Text.Json.JsonSerializer.Serialize(files);
    }

    private T GetValue<T>(string key, T defaultValue)
    {
        if (_cache.TryGetValue(key, out var value))
        {
            try
            {
                if (value is JsonElement je)
                {
                    return typeof(T) switch
                    {
                        Type t when t == typeof(double) => (T)(object)je.GetDouble(),
                        Type t when t == typeof(bool) => (T)(object)je.GetBoolean(),
                        Type t when t == typeof(string) => (T)(object)(je.GetString() ?? defaultValue?.ToString() ?? ""),
                        _ => defaultValue
                    };
                }
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch { }
        }
        return defaultValue;
    }

    private void SetValue(string key, object value) => _cache[key] = value;

    private Dictionary<string, object> Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? [];
            }
        }
        catch { }
        return [];
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }
}
