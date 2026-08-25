using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeDetect.Net;

/// <summary>Atlassian Statuspage 格式解析（迁移前官方状态页的兼容路径）。</summary>
public static class StatuspageParser
{
    public static ServiceStatusSnapshot SummaryFrom(byte[] data)
    {
        var payload = JsonSerializer.Deserialize<StatuspageSummaryPayload>(data, JsonDefaults.Options)
            ?? throw new InvalidOperationException("Failed to decode status summary.");
        var incidents = new List<StatuspageIncidentData>(payload.Incidents);
        foreach (var maintenance in payload.ScheduledMaintenances ?? new List<StatuspageIncidentData>())
        {
            if (!string.Equals(maintenance.Status, "completed", StringComparison.OrdinalIgnoreCase))
                incidents.Add(maintenance);
        }
        return MakeSnapshot(payload.Status, payload.Components, incidents);
    }

    public static ServiceStatusSnapshot CombinedFrom(byte[] statusData, byte[] componentsData, byte[] incidentsData)
    {
        var status = JsonSerializer.Deserialize<StatusEnvelope>(statusData, JsonDefaults.Options)?.Status
            ?? throw new InvalidOperationException("Failed to decode status.");
        var components = JsonSerializer.Deserialize<ComponentsEnvelope>(componentsData, JsonDefaults.Options)?.Components
            ?? throw new InvalidOperationException("Failed to decode components.");
        var incidents = JsonSerializer.Deserialize<IncidentsEnvelope>(incidentsData, JsonDefaults.Options)?.Incidents
            ?? throw new InvalidOperationException("Failed to decode incidents.");
        return MakeSnapshot(status, components, incidents);
    }

    public static bool Matches(string name, MonitoredServiceKind kind)
    {
        string normalized = name.ToLowerInvariant().Replace("-", " ").Replace("_", " ");
        if (kind == MonitoredServiceKind.Api)
            return normalized.Contains("api") || normalized.Contains("接口");
        return (normalized.Contains("web") && normalized.Contains("chat"))
            || normalized.Contains("网页对话") || normalized.Contains("网页聊天");
    }

    private static ServiceStatusSnapshot MakeSnapshot(
        StatusData status, List<StatuspageComponentData> components, List<StatuspageIncidentData> incidents)
    {
        var services = new List<MonitoredService>();
        foreach (var kind in new[] { MonitoredServiceKind.Api, MonitoredServiceKind.WebChat })
        {
            var component = components.Find(c => Matches(c.Name, kind));
            services.Add(new MonitoredService(
                kind,
                component?.Name ?? "",
                component == null ? ServiceHealth.Unknown : ServiceHealthExtensions.ComponentStatus(component.Status)));
        }
        return new ServiceStatusSnapshot(
            ServiceHealthExtensions.OverallIndicator(status.Indicator),
            services,
            incidents.ConvertAll(i => new ServiceIncident(i.Id, i.Name, i.Status)));
    }

    private sealed class StatusData
    {
        [JsonPropertyName("indicator")] public string Indicator { get; set; } = "";
    }

    private sealed class StatuspageComponentData
    {
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
    }

    private sealed class StatuspageIncidentData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
    }

    private sealed class StatuspageSummaryPayload
    {
        public StatusData Status { get; set; } = new();
        public List<StatuspageComponentData> Components { get; set; } = new();
        public List<StatuspageIncidentData> Incidents { get; set; } = new();
        [JsonPropertyName("scheduled_maintenances")] public List<StatuspageIncidentData>? ScheduledMaintenances { get; set; }
    }

    private sealed class StatusEnvelope { public StatusData Status { get; set; } = new(); }
    private sealed class ComponentsEnvelope { public List<StatuspageComponentData> Components { get; set; } = new(); }
    private sealed class IncidentsEnvelope { public List<StatuspageIncidentData> Incidents { get; set; } = new(); }
}
