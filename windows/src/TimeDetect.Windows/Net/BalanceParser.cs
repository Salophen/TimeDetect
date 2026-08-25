using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace TimeDetect.Net;

public enum BalanceParseError { InvalidAmount }

public sealed class BalanceParseException : Exception
{
    public BalanceParseError Error { get; }
    public BalanceParseException(BalanceParseError error) : base(error.ToString()) { Error = error; }
}

public static class BalanceParser
{
    public static BalanceSnapshot Parse(byte[] data)
    {
        var payload = JsonSerializer.Deserialize<BalancePayload>(data, JsonDefaults.Options)
            ?? throw new BalanceParseException(BalanceParseError.InvalidAmount);
        var infos = new List<BalanceInfo>();
        foreach (var info in payload.BalanceInfos)
        {
            if (!decimal.TryParse(info.TotalBalance, NumberStyles.Number, CultureInfo.InvariantCulture, out var total)
                || !decimal.TryParse(info.GrantedBalance, NumberStyles.Number, CultureInfo.InvariantCulture, out var granted)
                || !decimal.TryParse(info.ToppedUpBalance, NumberStyles.Number, CultureInfo.InvariantCulture, out var toppedUp))
            {
                throw new BalanceParseException(BalanceParseError.InvalidAmount);
            }
            infos.Add(new BalanceInfo(info.Currency.ToUpperInvariant(), total, granted, toppedUp));
        }
        return new BalanceSnapshot(payload.IsAvailable, infos);
    }
}

public static class DeepSeekBalanceAPI
{
    public static readonly Uri Endpoint = new("https://api.deepseek.com/user/balance");

    public static async Task<BalanceSnapshot> FetchAsync(
        IHTTPClient client, string apiKey, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var response = await client.SendAsync(request, cancellationToken);
        switch (response.StatusCode)
        {
            case 200:
                try { return BalanceParser.Parse(response.Data); }
                catch { throw new BalanceAPIException(BalanceAPIError.MalformedResponse); }
            case 401: throw new BalanceAPIException(BalanceAPIError.InvalidKey);
            case 402: throw new BalanceAPIException(BalanceAPIError.InsufficientBalance);
            case 429: throw new BalanceAPIException(BalanceAPIError.RateLimited);
            case 500:
            case 503: throw new BalanceAPIException(BalanceAPIError.ServiceUnavailable, response.StatusCode);
            default: throw new BalanceAPIException(BalanceAPIError.UnexpectedHTTP, response.StatusCode);
        }
    }
}

internal sealed class BalancePayload
{
    [JsonRequired] [JsonPropertyName("is_available")] public bool IsAvailable { get; set; }
    [JsonRequired] [JsonPropertyName("balance_infos")] public List<BalanceInfoData> BalanceInfos { get; set; } = new();

    internal sealed class BalanceInfoData
    {
        public string Currency { get; set; } = "";
        [JsonPropertyName("total_balance")] public string TotalBalance { get; set; } = "";
        [JsonPropertyName("granted_balance")] public string GrantedBalance { get; set; } = "";
        [JsonPropertyName("topped_up_balance")] public string ToppedUpBalance { get; set; } = "";
    }
}
