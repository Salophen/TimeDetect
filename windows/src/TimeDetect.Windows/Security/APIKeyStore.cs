using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace TimeDetect.Security;

public interface IAPIKeyStore
{
    Task<string?> ReadAsync();
    Task SaveAsync(string value);
    Task DeleteAsync();
}

/// <summary>
/// API Key 的本地存储。等价 macOS 版「UserDefaults 明文存储」的策略：
/// 写入本 App 数据目录下的 JSON 文件，不依赖系统钥匙串，启动时不会弹认证窗口。
/// 如需要更强的本地保护，可替换为 DPAPI（CryptProtectData）实现。
/// </summary>
public sealed class JsonFileAPIKeyStore : IAPIKeyStore
{
    private readonly string _filePath;

    public JsonFileAPIKeyStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppDataDirectory, "api-key.json");
    }

    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimeDetect");

    public Task<string?> ReadAsync()
    {
        if (!File.Exists(_filePath)) return Task.FromResult<string?>(null);
        try
        {
            var value = JsonSerializer.Deserialize<StoredKey>(File.ReadAllText(_filePath))?.Value;
            return Task.FromResult(string.IsNullOrEmpty(value) ? null : value);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task SaveAsync(string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(new StoredKey(value)));
        return Task.CompletedTask;
    }

    public Task DeleteAsync()
    {
        if (File.Exists(_filePath)) File.Delete(_filePath);
        return Task.CompletedTask;
    }

    private sealed class StoredKey
    {
        public string? Value { get; set; }
        public StoredKey() { }
        public StoredKey(string value) => Value = value;
    }
}
