using System;
using System.Collections.Generic;
using System.Globalization;

namespace TimeDetect.Net;

public sealed record BalanceInfo(string Currency, decimal Total, decimal Granted, decimal ToppedUp)
{
    public string TotalText => AmountText(Total);

    public string AmountText(decimal amount)
    {
        string symbol = Currency == "CNY" ? "¥" : (Currency == "USD" ? "$" : Currency + " ");
        return symbol + amount.ToString("0.00########", CultureInfo.InvariantCulture);
    }
}

public sealed record BalanceSnapshot(bool IsAvailable, IReadOnlyList<BalanceInfo> Balances);

public enum BalanceAPIError
{
    InvalidKey,
    InsufficientBalance,
    RateLimited,
    ServiceUnavailable,
    MalformedResponse,
    UnexpectedHTTP
}

public sealed class BalanceAPIException : Exception
{
    public BalanceAPIError Error { get; }
    public int? StatusCode { get; }

    public BalanceAPIException(BalanceAPIError error, int? statusCode = null)
        : base(statusCode == null ? error.ToString() : $"{error} ({statusCode})")
    {
        Error = error;
        StatusCode = statusCode;
    }
}
