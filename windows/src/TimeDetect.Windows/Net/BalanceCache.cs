using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TimeDetect.Security;

namespace TimeDetect.Net;

public sealed record CachedBalance(BalanceSnapshot Snapshot, DateTimeOffset LastUpdated, string KeySuffix);

public interface IBalanceCache
{
    Task<CachedBalance?> LoadAsync();
    Task SaveAsync(CachedBalance value);
    Task ClearAsync();
}

/// <summary>余额快照的持久化缓存，等价 macOS 版 UserDefaults 中保存的最近一次余额。</summary>
public sealed class JsonFileBalanceCache : IBalanceCache
{
    private readonly string _filePath;

    public JsonFileBalanceCache(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(JsonFileAPIKeyStore.AppDataDirectory, "balance-cache.json");
    }

    public Task<CachedBalance?> LoadAsync()
    {
        if (!File.Exists(_filePath)) return Task.FromResult<CachedBalance?>(null);
        try
        {
            var dto = JsonSerializer.Deserialize<CachedBalanceDto>(File.ReadAllText(_filePath));
            if (dto == null) return Task.FromResult<CachedBalance?>(null);
            var balances = dto.Snapshot?.Balances ?? new List<BalanceInfo>();
            return Task.FromResult<CachedBalance?>(new CachedBalance(
                new BalanceSnapshot(dto.Snapshot?.IsAvailable ?? false, balances),
                dto.LastUpdated,
                dto.KeySuffix ?? ""));
        }
        catch
        {
            return Task.FromResult<CachedBalance?>(null);
        }
    }

    public Task SaveAsync(CachedBalance value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var dto = new CachedBalanceDto
        {
            Snapshot = new BalanceSnapshotDto
            {
                IsAvailable = value.Snapshot.IsAvailable,
                Balances = new List<BalanceInfo>(value.Snapshot.Balances)
            },
            LastUpdated = value.LastUpdated,
            KeySuffix = value.KeySuffix
        };
        File.WriteAllText(_filePath, JsonSerializer.Serialize(dto));
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        if (File.Exists(_filePath)) File.Delete(_filePath);
        return Task.CompletedTask;
    }

    private sealed class CachedBalanceDto
    {
        public BalanceSnapshotDto? Snapshot { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        [JsonPropertyName("key_suffix")] public string? KeySuffix { get; set; }
    }

    private sealed class BalanceSnapshotDto
    {
        [JsonPropertyName("is_available")] public bool IsAvailable { get; set; }
        public List<BalanceInfo> Balances { get; set; } = new();
    }
}
