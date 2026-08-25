using System;
using System.Threading.Tasks;
using TimeDetect.Net;
using TimeDetect.Security;
using Xunit;

namespace TimeDetect.Windows.Tests;

public class BalanceManagerTests
{
    [Fact]
    public async Task SavesValidatesAndDeletesKey()
    {
        var keyStore = new MockAPIKeyStore();
        var cache = new MockBalanceCache();
        var client = new MockHTTPClient(new MockResponse(TestData.Json(
            "{\"is_available\":true,\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"8.00\"," +
            "\"granted_balance\":\"3.00\",\"topped_up_balance\":\"5.00\"}]}"), 200));
        var manager = new DeepSeekBalanceManager(client, keyStore, cache);

        await manager.SaveAndValidateAsync("test-api-key");

        Assert.True(manager.IsConfigured);
        Assert.Equal("-key", manager.KeySuffix);
        Assert.Equal(8m, manager.Balance?.Balances[0].Total);
        Assert.Equal(BalanceDisplayState.Available, manager.State);
        Assert.Equal(8m, cache.Value?.Snapshot.Balances[0].Total);

        await manager.DeleteKeyAsync();

        Assert.False(manager.IsConfigured);
        Assert.Null(manager.KeySuffix);
        Assert.Null(manager.Balance);
        Assert.Null(manager.LastUpdated);
        Assert.Equal(BalanceDisplayState.Unconfigured, manager.State);
        Assert.Null(await keyStore.ReadAsync());
        Assert.Null(await cache.LoadAsync());

        manager.Stop();
    }

    [Fact]
    public async Task RestoresCachedBalanceWhileOffline()
    {
        var cached = new CachedBalance(
            new BalanceSnapshot(true, new[] { new BalanceInfo("CNY", 8m, 3m, 5m) }),
            DateTimeOffset.Now, "-key");
        var keyStore = new MockAPIKeyStore("test-api-key");
        var cache = new MockBalanceCache(cached);
        var manager = new DeepSeekBalanceManager(new MockHTTPClient(), keyStore, cache);

        await manager.StartAsync();

        Assert.True(manager.IsConfigured);
        Assert.Equal("-key", manager.KeySuffix);
        Assert.Equal(8m, manager.Balance?.Balances[0].Total);
        Assert.Equal(BalanceDisplayState.NetworkUnavailable, manager.State);

        manager.Stop();
    }

    [Fact]
    public async Task FailedDeletionRemainsRetryable()
    {
        var keyStore = new MockAPIKeyStore("retry-test-key", deleteFailures: 1);
        var manager = new DeepSeekBalanceManager(new MockHTTPClient(), keyStore, new MockBalanceCache());

        await manager.DeleteKeyAsync();

        Assert.Equal(BalanceDisplayState.KeychainError, manager.State);
        Assert.True(manager.CanRetryKeyDeletion);
        Assert.Equal("retry-test-key", await keyStore.ReadAsync());

        await manager.RetryKeyDeletionAsync();

        Assert.False(manager.CanRetryKeyDeletion);
        Assert.Null(await keyStore.ReadAsync());
        Assert.Equal(BalanceDisplayState.Unconfigured, manager.State);

        manager.Stop();
    }

    private sealed class MockAPIKeyStore : IAPIKeyStore
    {
        public string? Value { get; private set; }
        private int _remainingDeleteFailures;

        public MockAPIKeyStore(string? value = null, int deleteFailures = 0)
        {
            Value = value;
            _remainingDeleteFailures = deleteFailures;
        }

        public Task<string?> ReadAsync() => Task.FromResult(Value);

        public Task SaveAsync(string value)
        {
            Value = value;
            return Task.CompletedTask;
        }

        public Task DeleteAsync()
        {
            if (_remainingDeleteFailures > 0)
            {
                _remainingDeleteFailures--;
                throw new InvalidOperationException("mock delete failure");
            }
            Value = null;
            return Task.CompletedTask;
        }
    }

    private sealed class MockBalanceCache : IBalanceCache
    {
        public CachedBalance? Value { get; private set; }

        public MockBalanceCache(CachedBalance? value = null) => Value = value;

        public Task<CachedBalance?> LoadAsync() => Task.FromResult(Value);

        public Task SaveAsync(CachedBalance value)
        {
            Value = value;
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            Value = null;
            return Task.CompletedTask;
        }
    }
}
