using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TimeDetect.Net;

public sealed record HTTPResponse(byte[] Data, int StatusCode);

public interface IHTTPClient
{
    Task<HTTPResponse> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}

/// <summary>默认 HTTP 客户端：8 秒超时、不缓存，等价 macOS 版的 URLSession ephemeral 配置。</summary>
public sealed class DefaultHTTPClient : IHTTPClient
{
    private readonly HttpClient _http;

    public DefaultHTTPClient()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
    }

    public async Task<HTTPResponse> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new HTTPResponse(data, (int)response.StatusCode);
    }

    public void Dispose() => _http.Dispose();
}
