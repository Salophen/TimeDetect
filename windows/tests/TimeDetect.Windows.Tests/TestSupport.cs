using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TimeDetect.Net;

namespace TimeDetect.Windows.Tests;

public sealed record MockResponse(byte[] Data, int StatusCode = 200, bool ThrowsNetworkError = false);

public sealed class MockHTTPClient : IHTTPClient
{
    private readonly Queue<MockResponse> _queue;
    public List<HttpRequestMessage> Requests { get; } = new();

    public MockHTTPClient(IEnumerable<MockResponse> responses) => _queue = new Queue<MockResponse>(responses);
    public MockHTTPClient(params MockResponse[] responses) => _queue = new Queue<MockResponse>(responses);

    public async Task<HTTPResponse> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        // 模拟真实网络 I/O 的异步让出，避免同步完成导致「在途去重」逻辑失效。
        await Task.Yield();
        Requests.Add(request);
        if (_queue.Count == 0)
            throw new HttpRequestException("No more mock responses configured.");
        var next = _queue.Dequeue();
        if (next.ThrowsNetworkError)
            throw new HttpRequestException("Mock network failure.");
        return new HTTPResponse(next.Data, next.StatusCode);
    }
}

public static class TestData
{
    public static byte[] Json(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>构造与 macOS 测试一致的 Flashcat 服务端渲染页面。</summary>
    public static byte[] FlashcatPage(
        long pageId = 6_410_630_422_455,
        string domain = "status.deepseek.com",
        string? apiStatus = null,
        string? chatStatus = null,
        string activeChanges = "[]")
    {
        string StatusField(string? value) => value == null ? "" : $",\"status\":\"{value}\"";

        string payload =
            "{\"page\":{\"page_id\":" + pageId + ",\"name\":\"DeepSeek\",\"custom_domain\":\"" + domain + "\",\"components\":[" +
            "{\"component_id\":\"api-pro\",\"name\":\"DeepSeek V4 Pro API服务(API Service)\"" + StatusField(apiStatus) + "}," +
            "{\"component_id\":\"api-flash\",\"name\":\"DeepSeek V4 Flash API服务(API Service)\",\"status\":\"operational\"}," +
            "{\"component_id\":\"chat-instant\",\"section_id\":\"chat\",\"name\":\"快速模式(Instant Mode)\"" + StatusField(chatStatus) + "}," +
            "{\"component_id\":\"chat-search\",\"section_id\":\"chat\",\"name\":\"搜索服务(Search Service)\",\"status\":\"operational\"}]," +
            "\"sections\":[{\"section_id\":\"chat\",\"name\":\"对话服务(Chat Service)\"}]}," +
            "\"active_changes\":" + activeChanges + "}";

        string inner = "1e:[\"$\",\"component\",null,{\"initialData\":" + payload + "}]";
        string literal = JsonSerializer.Serialize(inner);
        string html = "<html><script>self.__next_f.push([1," + literal + "])</script></html>";
        return Encoding.UTF8.GetBytes(html);
    }
}
