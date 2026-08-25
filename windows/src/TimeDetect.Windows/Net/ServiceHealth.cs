using System.Collections.Generic;

namespace TimeDetect.Net;

public enum ServiceHealth
{
    Operational,
    Degraded,
    PartialOutage,
    MajorOutage,
    Maintenance,
    Unknown
}

public static class ServiceHealthExtensions
{
    public static ServiceHealth OverallIndicator(string value) => value.ToLowerInvariant() switch
    {
        "none" => ServiceHealth.Operational,
        "minor" => ServiceHealth.Degraded,
        "major" => ServiceHealth.PartialOutage,
        "critical" => ServiceHealth.MajorOutage,
        _ => ServiceHealth.Unknown
    };

    public static ServiceHealth ComponentStatus(string value) => value.ToLowerInvariant() switch
    {
        "operational" => ServiceHealth.Operational,
        "degraded_performance" => ServiceHealth.Degraded,
        "degraded" => ServiceHealth.Degraded,
        "partial_outage" => ServiceHealth.PartialOutage,
        "major_outage" => ServiceHealth.MajorOutage,
        "full_outage" => ServiceHealth.MajorOutage,
        "under_maintenance" => ServiceHealth.Maintenance,
        "maintenance" => ServiceHealth.Maintenance,
        _ => ServiceHealth.Unknown
    };

    /// <summary>聚合多个组件时保留最严重的状态；未知状态不能被误报为正常。</summary>
    public static ServiceHealth Worst(IEnumerable<ServiceHealth> values)
    {
        ServiceHealth? worst = null;
        foreach (var value in values)
        {
            if (worst == null || Severity(value) > Severity(worst.Value)) worst = value;
        }
        return worst ?? ServiceHealth.Unknown;
    }

    private static int Severity(ServiceHealth value) => value switch
    {
        ServiceHealth.Operational => 0,
        ServiceHealth.Maintenance => 1,
        ServiceHealth.Degraded => 2,
        ServiceHealth.PartialOutage => 3,
        ServiceHealth.MajorOutage => 4,
        _ => 5
    };

    public static string Title(this ServiceHealth value) => value switch
    {
        ServiceHealth.Operational => "服务正常",
        ServiceHealth.Degraded => "性能下降",
        ServiceHealth.PartialOutage => "部分中断",
        ServiceHealth.MajorOutage => "严重中断",
        ServiceHealth.Maintenance => "维护中",
        _ => "状态未知"
    };
}

public enum MonitoredServiceKind { Api, WebChat }

public sealed record MonitoredService(MonitoredServiceKind Kind, string OfficialName, ServiceHealth Health)
{
    public string DisplayName => Kind == MonitoredServiceKind.Api ? "API 服务" : "网页对话服务";
}

public sealed record ServiceIncident(string Id, string Name, string Status)
{
    public string StatusText => Status.ToLowerInvariant() switch
    {
        "investigating" => "正在调查",
        "identified" => "问题已确认",
        "monitoring" => "正在监控",
        "resolved" => "已解决",
        _ => Status
    };
}

public sealed record ServiceStatusSnapshot(
    ServiceHealth Overall,
    IReadOnlyList<MonitoredService> Services,
    IReadOnlyList<ServiceIncident> Incidents);
