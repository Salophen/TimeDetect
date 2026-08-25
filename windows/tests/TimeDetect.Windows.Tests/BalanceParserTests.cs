using System;
using System.Threading.Tasks;
using TimeDetect.Net;
using Xunit;

namespace TimeDetect.Windows.Tests;

public class BalanceParserTests
{
    [Fact]
    public void ParsesSingleCny()
    {
        var snapshot = BalanceParser.Parse(TestData.Json(
            "{\"is_available\":true,\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"110.00\"," +
            "\"granted_balance\":\"10.00\",\"topped_up_balance\":\"100.00\"}]}"));

        Assert.True(snapshot.IsAvailable);
        Assert.Single(snapshot.Balances);
        Assert.Equal(110.00m, snapshot.Balances[0].Total);
        Assert.Equal(10.00m, snapshot.Balances[0].Granted);
        Assert.Equal(100.00m, snapshot.Balances[0].ToppedUp);
        Assert.Equal("¥110.00", snapshot.Balances[0].TotalText);
    }

    [Fact]
    public void FormatsUsdAndMultipleCurrencies()
    {
        var usd = BalanceParser.Parse(TestData.Json(
            "{\"is_available\":true,\"balance_infos\":[{\"currency\":\"USD\",\"total_balance\":\"16.42\"," +
            "\"granted_balance\":\"1.42\",\"topped_up_balance\":\"15.00\"}]}"));
        Assert.Equal("$16.42", usd.Balances[0].TotalText);

        var multi = BalanceParser.Parse(TestData.Json(
            "{\"is_available\":false,\"balance_infos\":[" +
            "{\"currency\":\"CNY\",\"total_balance\":\"0.00\",\"granted_balance\":\"0.00\",\"topped_up_balance\":\"0.00\"}," +
            "{\"currency\":\"USD\",\"total_balance\":\"2.50\",\"granted_balance\":\"0.50\",\"topped_up_balance\":\"2.00\"}]}"));
        Assert.False(multi.IsAvailable);
        Assert.Equal(2, multi.Balances.Count);
    }

    [Fact]
    public void RejectsInvalidAmount()
    {
        Assert.Throws<BalanceParseException>(() => BalanceParser.Parse(TestData.Json(
            "{\"is_available\":true,\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"bad\"," +
            "\"granted_balance\":\"0\",\"topped_up_balance\":\"0\"}]}")));
    }

    [Theory]
    [InlineData(401, BalanceAPIError.InvalidKey)]
    [InlineData(402, BalanceAPIError.InsufficientBalance)]
    [InlineData(429, BalanceAPIError.RateLimited)]
    [InlineData(500, BalanceAPIError.ServiceUnavailable)]
    [InlineData(503, BalanceAPIError.ServiceUnavailable)]
    public async Task MapsHttpErrors(int status, BalanceAPIError expected)
    {
        var client = new MockHTTPClient(new MockResponse(Array.Empty<byte>(), status));
        var ex = await Assert.ThrowsAsync<BalanceAPIException>(() =>
            DeepSeekBalanceAPI.FetchAsync(client, "test-api-key"));
        Assert.Equal(expected, ex.Error);
    }

    [Fact]
    public async Task MalformedOkMapsToMalformedResponse()
    {
        var client = new MockHTTPClient(new MockResponse(TestData.Json("{}"), 200));
        var ex = await Assert.ThrowsAsync<BalanceAPIException>(() =>
            DeepSeekBalanceAPI.FetchAsync(client, "test-api-key"));
        Assert.Equal(BalanceAPIError.MalformedResponse, ex.Error);
    }

    [Fact]
    public async Task SendsKeyOnlyToOfficialHost()
    {
        var client = new MockHTTPClient(new MockResponse(
            TestData.Json("{\"is_available\":true,\"balance_infos\":[]}"), 200));
        await DeepSeekBalanceAPI.FetchAsync(client, "test-api-key");

        var request = Assert.Single(client.Requests);
        Assert.Equal("api.deepseek.com", request.RequestUri?.Host);
        Assert.Equal("Bearer test-api-key", request.Headers.Authorization?.ToString());
    }
}
