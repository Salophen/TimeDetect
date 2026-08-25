using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace TimeDetect.Net;

/// <summary>DeepSeek 官方服务状态轮询（60 秒）。</summary>
public sealed class DeepSeekStatusManager : INotifyPropertyChanged
{
    private readonly IHTTPClient _client;
    private readonly object _refreshLock = new();
    private CancellationTokenSource? _monitorCts;
    private CancellationTokenSource? _requestCts;
    private Task? _refreshTask;

    private ServiceStatusSnapshot? _snapshot;
    private bool _isRefreshing;
    private bool _isTemporarilyUnavailable;
    private DateTimeOffset? _lastUpdated;

    public static readonly Uri OfficialPageURL = new("https://status.deepseek.com");

    public ServiceStatusSnapshot? Snapshot
    {
        get => _snapshot;
        private set => SetField(ref _snapshot, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetField(ref _isRefreshing, value);
    }

    public bool IsTemporarilyUnavailable
    {
        get => _isTemporarilyUnavailable;
        private set => SetField(ref _isTemporarilyUnavailable, value);
    }

    public DateTimeOffset? LastUpdated
    {
        get => _lastUpdated;
        private set => SetField(ref _lastUpdated, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeepSeekStatusManager(IHTTPClient? client = null)
    {
        _client = client ?? new DefaultHTTPClient();
    }

    public void Start()
    {
        if (_monitorCts != null) return;
        Refresh();
        _monitorCts = new CancellationTokenSource();
        var ct = _monitorCts.Token;
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(60), ct); }
                catch (OperationCanceledException) { return; }
                Refresh();
            }
        }, ct);
    }

    public void Refresh() => _ = RefreshAsync();

    /// <summary>返回可等待的任务，便于测试确定性等待；在途重复请求会被忽略。</summary>
    public Task RefreshAsync()
    {
        lock (_refreshLock)
        {
            if (_refreshTask is { IsCompleted: false }) return Task.CompletedTask;

            var requestCts = new CancellationTokenSource();
            _requestCts = requestCts;
            _refreshTask = RefreshCoreAsync(requestCts);
            return _refreshTask;
        }
    }

    private async Task RefreshCoreAsync(CancellationTokenSource requestCts)
    {
        _isRefreshing = true;
        OnPropertyChanged(nameof(IsRefreshing));

        var ct = requestCts.Token;

        try
        {
            var value = await FetchAsync(_client, ct);
            if (ct.IsCancellationRequested) return;
            Snapshot = value;
            LastUpdated = DateTimeOffset.Now;
            IsTemporarilyUnavailable = false;
        }
        catch (OperationCanceledException) { }
        catch
        {
            // 网络、HTTP 或解码失败都不代表 DeepSeek 宕机；保留最后成功数据。
            IsTemporarilyUnavailable = true;
        }
        finally
        {
            bool ownsRefreshState;
            lock (_refreshLock)
            {
                ownsRefreshState = ReferenceEquals(_requestCts, requestCts);
                if (ownsRefreshState)
                {
                    _requestCts = null;
                    _refreshTask = null;
                }
            }
            if (ownsRefreshState)
            {
                _isRefreshing = false;
                OnPropertyChanged(nameof(IsRefreshing));
            }
        }
    }

    public void Stop()
    {
        _monitorCts?.Cancel();
        lock (_refreshLock)
        {
            _requestCts?.Cancel();
            _requestCts = null;
            _refreshTask = null;
        }
        _monitorCts = null;
        IsRefreshing = false;
    }

    public bool IsStale(DateTimeOffset? now = null)
    {
        if (_lastUpdated == null) return false;
        return (now ?? DateTimeOffset.Now) - _lastUpdated.Value > TimeSpan.FromMinutes(5);
    }

    public static async Task<ServiceStatusSnapshot> FetchAsync(
        IHTTPClient client, CancellationToken cancellationToken = default)
    {
        try
        {
            // DeepSeek 已将状态页迁移到 Flashcat。使用供应商公开 slug 可绕过
            // status.deepseek.com 当前会重置 TLS 连接的问题。
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://statuspage.flashcat.cloud/deepseek");
            request.Headers.TryAddWithoutValidation("Accept", "text/html");
            var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode < 200 || response.StatusCode >= 300)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}");
            return FlashcatParser.FlashcatPageFrom(response.Data);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 保留迁移前 Atlassian Statuspage JSON 的兼容路径。
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://status.deepseek.com/api/v2/summary.json");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode < 200 || response.StatusCode >= 300)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}");
            return StatuspageParser.SummaryFrom(response.Data);
        }
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
