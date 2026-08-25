using System;
using System.Threading.Tasks;
using TimeDetect.Net;
using Xunit;

namespace TimeDetect.Windows.Tests;

public class DeepSeekStatusManagerTests
{
    [Fact]
    public async Task PublishesSnapshotAndDeduplicatesInFlight()
    {
        var client = new MockHTTPClient(new MockResponse(
            TestData.FlashcatPage(apiStatus: "degraded"), 200));
        client.RequestStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.RequestRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = new DeepSeekStatusManager(client);

        var first = manager.RefreshAsync();
        await client.RequestStarted.Task;
        var second = manager.RefreshAsync(); // 在途去重
        client.RequestRelease.TrySetResult(true);
        await first;
        await second;

        Assert.Equal(ServiceHealth.Degraded, manager.Snapshot?.Overall);
        Assert.NotNull(manager.LastUpdated);
        Assert.Single(client.Requests);
        Assert.Equal("https://statuspage.flashcat.cloud/deepseek",
            client.Requests[0].RequestUri?.AbsoluteUri);
        manager.Stop();
    }

    [Fact]
    public async Task FallsBackToLegacySummary()
    {
        var client = new MockHTTPClient(
            new MockResponse(Array.Empty<byte>(), ThrowsNetworkError: true),
            new MockResponse(TestData.Json(
                "{\"status\":{\"indicator\":\"minor\"},\"components\":[" +
                "{\"name\":\"API Service\",\"status\":\"degraded_performance\"}," +
                "{\"name\":\"Web Chat Service\",\"status\":\"operational\"}],\"incidents\":[]}"), 200));

        var snapshot = await DeepSeekStatusManager.FetchAsync(client);

        Assert.Equal(ServiceHealth.Degraded, snapshot.Overall);
        Assert.Equal(2, client.Requests.Count);
        Assert.Equal("https://status.deepseek.com/api/v2/summary.json",
            client.Requests[1].RequestUri?.AbsoluteUri);
    }
}
