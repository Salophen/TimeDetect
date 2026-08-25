namespace TimeDetect.Net;

public enum BalanceDisplayState
{
    Unconfigured,
    Loading,
    Available,
    Insufficient,
    InvalidKey,
    NetworkUnavailable,
    RateLimited,
    ServiceError,
    MalformedResponse,
    KeychainError
}

public static class BalanceDisplayStateExtensions
{
    public static string Message(this BalanceDisplayState state) => state switch
    {
        BalanceDisplayState.Unconfigured => "尚未配置 API Key",
        BalanceDisplayState.Loading => "正在查询余额",
        BalanceDisplayState.Available => "可正常调用",
        BalanceDisplayState.Insufficient => "API 余额不足",
        BalanceDisplayState.InvalidKey => "API Key 无效",
        BalanceDisplayState.NetworkUnavailable => "当前网络不可用",
        BalanceDisplayState.RateLimited => "请求过于频繁，请稍后重试",
        BalanceDisplayState.ServiceError => "DeepSeek 服务暂时不可用",
        BalanceDisplayState.MalformedResponse => "余额数据格式异常",
        BalanceDisplayState.KeychainError => "无法访问本机凭据存储",
        _ => state.ToString()
    };
}
