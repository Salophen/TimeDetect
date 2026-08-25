using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TimeDetect.Security;

namespace TimeDetect.Net;

/// <summary>API 余额查询与缓存（配置后每 5 分钟轮询）。</summary>
public sealed class DeepSeekBalanceManager : INotifyPropertyChanged
{
    private readonly IHTTPClient _client;
    private readonly IAPIKeyStore _keyStore;
    private readonly IBalanceCache _balanceCache;

    private string? _apiKey;
    private CancellationTokenSource? _monitorCts;
    private CancellationTokenSource? _requestCts;
    private CancellationTokenSource? _startupCts;
    private CancellationTokenSource? _deletionCts;

    private BalanceSnapshot? _balance;
    private BalanceDisplayState _state = BalanceDisplayState.Unconfigured;
    private bool _isRefreshing;
    private DateTimeOffset? _lastUpdated;
    private string? _keySuffix;
    private bool _isDeletingKey;
    private bool _canRetryKeyDeletion;

    public bool IsConfigured => _apiKey != null;

    public BalanceSnapshot? Balance { get => _balance; private set => SetField(ref _balance, value); }
    public BalanceDisplayState State { get => _state; private set => SetField(ref _state, value); }
    public bool IsRefreshing { get => _isRefreshing; private set => SetField(ref _isRefreshing, value); }
    public DateTimeOffset? LastUpdated { get => _lastUpdated; private set => SetField(ref _lastUpdated, value); }
    public string? KeySuffix { get => _keySuffix; private set => SetField(ref _keySuffix, value); }
    public bool IsDeletingKey { get => _isDeletingKey; private set => SetField(ref _isDeletingKey, value); }
    public bool CanRetryKeyDeletion { get => _canRetryKeyDeletion; private set => SetField(ref _canRetryKeyDeletion, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeepSeekBalanceManager(
        IHTTPClient? client = null,
        IAPIKeyStore? keyStore = null,
        IBalanceCache? balanceCache = null)
    {
        _client = client ?? new DefaultHTTPClient();
        _keyStore = keyStore ?? new JsonFileAPIKeyStore();
        _balanceCache = balanceCache ?? new JsonFileBalanceCache();
    }

    public void Start() => _ = StartAsync();

    /// <summary>恢复本机保存的 API Key 和最近一次成功余额，并在后台查询最新余额。</summary>
    public async Task StartAsync()
    {
        if (_apiKey != null || _startupCts != null) return;
        _startupCts = new CancellationTokenSource();
        var ct = _startupCts.Token;
        try
        {
            var storedKey = await _keyStore.ReadAsync();
            if (storedKey == null || ct.IsCancellationRequested) return;
            SetConfiguredKey(storedKey);
            var cached = await _balanceCache.LoadAsync();
            if (cached != null && cached.KeySuffix == _keySuffix && !ct.IsCancellationRequested)
            {
                Balance = cached.Snapshot;
                LastUpdated = cached.LastUpdated;
                State = cached.Snapshot.IsAvailable ? BalanceDisplayState.Available : BalanceDisplayState.Insufficient;
            }
            else
            {
                State = BalanceDisplayState.Loading;
            }
            StartMonitoringIfNeeded();
            await RefreshAsync(force: true);
        }
        catch (OperationCanceledException) { }
        catch { State = BalanceDisplayState.KeychainError; }
        finally { _startupCts = null; }
    }

    public void Refresh(bool force = false) => _ = RefreshAsync(force);

    public async Task RefreshAsync(bool force = false)
    {
        if (_apiKey == null || _isRefreshing) return;
        if (!force && _lastUpdated != null && (DateTimeOffset.Now - _lastUpdated.Value).TotalSeconds < 60) return;

        _isRefreshing = true;
        OnPropertyChanged(nameof(IsRefreshing));
        if (Balance == null) State = BalanceDisplayState.Loading;

        var client = _client;
        var apiKey = _apiKey;
        _requestCts?.Cancel();
        _requestCts = new CancellationTokenSource();
        var ct = _requestCts.Token;

        try
        {
            var value = await DeepSeekBalanceAPI.FetchAsync(client, apiKey, ct);
            if (ct.IsCancellationRequested) return;
            Balance = value;
            var updatedAt = DateTimeOffset.Now;
            LastUpdated = updatedAt;
            State = value.IsAvailable ? BalanceDisplayState.Available : BalanceDisplayState.Insufficient;
            if (_keySuffix != null)
                await _balanceCache.SaveAsync(new CachedBalance(value, updatedAt, _keySuffix));
        }
        catch (OperationCanceledException) { }
        catch (BalanceAPIException e) { Apply(e.Error); }
        catch { State = BalanceDisplayState.NetworkUnavailable; }
        finally
        {
            if (_requestCts?.Token == ct)
            {
                _isRefreshing = false;
                OnPropertyChanged(nameof(IsRefreshing));
            }
        }
    }

    public async Task RefreshIfStaleAsync(double maxAgeSeconds = 60)
    {
        if (!IsConfigured) return;
        if (_lastUpdated != null && (DateTimeOffset.Now - _lastUpdated.Value).TotalSeconds < maxAgeSeconds) return;
        await RefreshAsync(force: true);
    }
    public async Task SaveAndValidateAsync(string value)
    {
        if (_isDeletingKey) return;
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return;

        _startupCts?.Cancel();
        _requestCts?.Cancel();

        try
        {
            await _keyStore.SaveAsync(trimmed);
            SetConfiguredKey(trimmed);
            CanRetryKeyDeletion = false;
            Balance = null;
            LastUpdated = null;
            State = BalanceDisplayState.Loading;
            await _balanceCache.ClearAsync();
            StartMonitoringIfNeeded();
            await RefreshAsync(force: true);
        }
        catch (OperationCanceledException) { }
        catch { State = BalanceDisplayState.KeychainError; }
    }

    public void DeleteKey() => _ = DeleteKeyAsync();

    public async Task DeleteKeyAsync()
    {
        if (_isDeletingKey) return;
        _startupCts?.Cancel();
        _requestCts?.Cancel();
        _monitorCts?.Cancel();
        _monitorCts = null;
        ClearConfiguration();
        IsDeletingKey = true;
        CanRetryKeyDeletion = false;

        _deletionCts = new CancellationTokenSource();
        var ct = _deletionCts.Token;
        try
        {
            await _balanceCache.ClearAsync();
            await _keyStore.DeleteAsync();
            if (ct.IsCancellationRequested) return;
            // 删除后回读验证，避免仅凭删除调用未报错就向用户宣告成功。
            if (await _keyStore.ReadAsync() != null)
                throw new InvalidOperationException("deletion verification failed");
        }
        catch (OperationCanceledException) { }
        catch
        {
            State = BalanceDisplayState.KeychainError;
            CanRetryKeyDeletion = true;
        }
        finally
        {
            IsDeletingKey = false;
            _deletionCts = null;
        }
    }

    public void RetryKeyDeletion() => _ = RetryKeyDeletionAsync();

    public async Task RetryKeyDeletionAsync()
    {
        if (!CanRetryKeyDeletion || _isDeletingKey) return;
        await DeleteKeyAsync();
    }

    public void Stop()
    {
        _startupCts?.Cancel();
        _deletionCts?.Cancel();
        _monitorCts?.Cancel();
        _requestCts?.Cancel();
        _startupCts = null;
        _monitorCts = null;
        _requestCts = null;
        _deletionCts = null;
        _isRefreshing = false;
        _isDeletingKey = false;
        OnPropertyChanged(nameof(IsRefreshing));
        OnPropertyChanged(nameof(IsDeletingKey));
    }

    private void StartMonitoringIfNeeded()
    {
        if (_monitorCts != null) return;
        _monitorCts = new CancellationTokenSource();
        var ct = _monitorCts.Token;
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromMinutes(5), ct); }
                catch (OperationCanceledException) { return; }
                await RefreshAsync(force: true);
            }
        }, ct);
    }

    private void ClearConfiguration()
    {
        _apiKey = null;
        KeySuffix = null;
        Balance = null;
        LastUpdated = null;
        _isRefreshing = false;
        State = BalanceDisplayState.Unconfigured;
        _monitorCts = null;
        _requestCts = null;
        OnPropertyChanged(nameof(IsConfigured));
    }

    private void SetConfiguredKey(string value)
    {
        _apiKey = value;
        KeySuffix = value.Length > 4 ? value.Substring(value.Length - 4) : value;
        OnPropertyChanged(nameof(IsConfigured));
    }

    private void Apply(BalanceAPIError error)
    {
        State = error switch
        {
            BalanceAPIError.InvalidKey => BalanceDisplayState.InvalidKey,
            BalanceAPIError.InsufficientBalance => BalanceDisplayState.Insufficient,
            BalanceAPIError.RateLimited => BalanceDisplayState.RateLimited,
            BalanceAPIError.ServiceUnavailable => BalanceDisplayState.ServiceError,
            BalanceAPIError.UnexpectedHTTP => BalanceDisplayState.ServiceError,
            _ => BalanceDisplayState.MalformedResponse
        };
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
