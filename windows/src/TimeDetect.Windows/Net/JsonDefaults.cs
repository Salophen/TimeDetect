using System.Text.Json;

namespace TimeDetect.Net;

/// <summary>网络解析共用的 JSON 反序列化选项（键名不区分大小写）。</summary>
internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
