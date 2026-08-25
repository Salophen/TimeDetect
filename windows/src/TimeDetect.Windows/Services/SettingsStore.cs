using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TimeDetect.Services;

/// <summary>简单的 JSON 设置存储，等价 macOS 版 UserDefaults。</summary>
public sealed class SettingsStore
{
    private readonly string _filePath;
    private readonly Dictionary<string, JsonElement> _values = new();

    public SettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppDataDirectory, "settings.json");
        Load();
    }

    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimeDetect");

    public string? GetString(string key)
    {
        if (_values.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String) return v.GetString();
        return null;
    }

    public bool GetBool(string key, bool fallback = false)
    {
        if (_values.TryGetValue(key, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False))
            return v.GetBoolean();
        return fallback;
    }

    public int GetInt(string key, int fallback = 0)
    {
        if (_values.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n))
            return n;
        return fallback;
    }

    public void Set(string key, string value) => SetCore(key, JsonSerializer.SerializeToElement(value));
    public void Set(string key, bool value) => SetCore(key, JsonSerializer.SerializeToElement(value));
    public void Set(string key, int value) => SetCore(key, JsonSerializer.SerializeToElement(value));

    private void SetCore(string key, JsonElement element)
    {
        _values[key] = element;
        Save();
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(_filePath));
            foreach (var prop in doc.RootElement.EnumerateObject())
                _values[prop.Name] = prop.Value.Clone();
        }
        catch
        {
            // 忽略损坏的设置文件，使用默认值。
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            using var stream = File.Create(_filePath);
            using var writer = new Utf8JsonWriter(stream);
            writer.WriteStartObject();
            foreach (var kv in _values)
            {
                writer.WritePropertyName(kv.Key);
                kv.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        catch
        {
            // 写入失败不影响运行。
        }
    }
}
