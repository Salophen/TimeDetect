using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeDetect.Net;

public enum FlashcatParseError { InvalidPayload, UnexpectedPage }

public sealed class FlashcatParseException : Exception
{
    public FlashcatParseError Error { get; }
    public FlashcatParseException(FlashcatParseError error) : base(error.ToString()) { Error = error; }
}

/// <summary>
/// Flashcat 状态页由 Next.js 服务端渲染，公开状态数据以 JSON 形式放在
/// <c>self.__next_f.push</c> 的 <c>initialData</c> 中。这里只解析该结构化 JSON，
/// 不根据页面文案或「可访问」与否猜测服务状态。
/// </summary>
public static class FlashcatParser
{
    private const long DeepSeekFlashcatPageID = 6_410_630_422_455;

    public static ServiceStatusSnapshot FlashcatPageFrom(byte[] data)
    {
        if (data.Length > 2_000_000) throw new FlashcatParseException(FlashcatParseError.InvalidPayload);
        string html;
        try { html = Encoding.UTF8.GetString(data); }
        catch { throw new FlashcatParseException(FlashcatParseError.InvalidPayload); }

        var payloadData = NextInitialData(html)
            ?? throw new FlashcatParseException(FlashcatParseError.InvalidPayload);
        var payload = JsonSerializer.Deserialize<FlashcatPayload>(payloadData, JsonDefaults.Options)
            ?? throw new FlashcatParseException(FlashcatParseError.InvalidPayload);

        if (payload.Page.PageID != DeepSeekFlashcatPageID
            || !string.Equals(payload.Page.Name, "DeepSeek", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(payload.Page.CustomDomain, "status.deepseek.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new FlashcatParseException(FlashcatParseError.UnexpectedPage);
        }

        var currentStatuses = new Dictionary<string, ServiceHealth>();
        foreach (var component in payload.Page.Components)
        {
            currentStatuses[component.ComponentID] =
                ServiceHealthExtensions.ComponentStatus(component.Status ?? "operational");
        }
        foreach (var change in payload.ActiveChanges)
        {
            foreach (var component in change.AffectedComponents)
            {
                currentStatuses[component.ComponentID] =
                    ServiceHealthExtensions.ComponentStatus(component.Status ?? "unknown");
            }
        }

        var chatSectionIDs = new HashSet<string>();
        foreach (var section in payload.Page.Sections)
        {
            string name = section.Name.ToLowerInvariant();
            if (name.Contains("chat") || name.Contains("对话") || name.Contains("聊天"))
                chatSectionIDs.Add(section.SectionID);
        }

        var apiComponents = payload.Page.Components
            .FindAll(c => StatuspageParser.Matches(c.Name, MonitoredServiceKind.Api));
        var chatComponents = payload.Page.Components.FindAll(c =>
        {
            if (c.SectionID == null) return StatuspageParser.Matches(c.Name, MonitoredServiceKind.WebChat);
            return chatSectionIDs.Contains(c.SectionID) || StatuspageParser.Matches(c.Name, MonitoredServiceKind.WebChat);
        });

        var services = new List<MonitoredService>
        {
            FlashcatService(MonitoredServiceKind.Api, apiComponents, currentStatuses),
            FlashcatService(MonitoredServiceKind.WebChat, chatComponents, currentStatuses)
        };

        var allHealth = payload.Page.Components.ConvertAll(
            c => currentStatuses.GetValueOrDefault(c.ComponentID, ServiceHealth.Unknown));

        return new ServiceStatusSnapshot(
            ServiceHealthExtensions.Worst(allHealth),
            services,
            payload.ActiveChanges.ConvertAll(ch => new ServiceIncident(ch.ChangeID.ToString(), ch.Title, ch.Status)));
    }

    private static MonitoredService FlashcatService(
        MonitoredServiceKind kind,
        List<FlashcatComponentData> components,
        Dictionary<string, ServiceHealth> statuses)
    {
        var healths = components.ConvertAll(c => statuses.GetValueOrDefault(c.ComponentID, ServiceHealth.Unknown));
        return new MonitoredService(
            kind,
            string.Join(", ", components.ConvertAll(c => c.Name)),
            ServiceHealthExtensions.Worst(healths));
    }
    private static byte[]? NextInitialData(string html)
    {
        const string pushMarker = "self.__next_f.push([1,";
        const string dataMarker = "\"initialData\":";
        int searchStart = 0;
        while (searchStart < html.Length)
        {
            int markerIndex = html.IndexOf(pushMarker, searchStart, StringComparison.Ordinal);
            if (markerIndex < 0) return null;
            int afterMarker = markerIndex + pushMarker.Length;
            int quoteIndex = html.IndexOf('"', afterMarker);
            if (quoteIndex < 0) return null;
            int literalEnd = JsonStringEnd(html, quoteIndex);
            if (literalEnd < 0) return null;
            string literal = html.Substring(quoteIndex, literalEnd - quoteIndex + 1);
            string[]? array = JsonSerializer.Deserialize<string[]>("[" + literal + "]", JsonDefaults.Options);
            if (array != null && array.Length > 0)
            {
                string text = array[0];
                int marker = text.IndexOf(dataMarker, StringComparison.Ordinal);
                if (marker >= 0)
                {
                    int objectStart = text.IndexOf('{', marker + dataMarker.Length);
                    if (objectStart >= 0)
                    {
                        int objectEnd = JsonObjectEnd(text, objectStart);
                        if (objectEnd >= 0)
                            return Encoding.UTF8.GetBytes(text.Substring(objectStart, objectEnd - objectStart + 1));
                    }
                }
            }
            searchStart = literalEnd + 1;
        }
        return null;
    }

    private static int JsonStringEnd(string text, int start)
    {
        int index = start + 1;
        bool escaped = false;
        while (index < text.Length)
        {
            char c = text[index];
            if (escaped) escaped = false;
            else if (c == '\\') escaped = true;
            else if (c == '"') return index;
            index++;
        }
        return -1;
    }

    private static int JsonObjectEnd(string text, int start)
    {
        int index = start;
        int depth = 0;
        bool inString = false;
        bool escaped = false;
        while (index < text.Length)
        {
            char c = text[index];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
            }
            else if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return index;
            }
            index++;
        }
        return -1;
    }

    private sealed class FlashcatPayload
    {
        public FlashcatPageData Page { get; set; } = new();
        [JsonPropertyName("active_changes")] public List<FlashcatChangeData> ActiveChanges { get; set; } = new();
    }

    private sealed class FlashcatPageData
    {
        [JsonPropertyName("page_id")] public long PageID { get; set; }
        public string Name { get; set; } = "";
        [JsonPropertyName("custom_domain")] public string CustomDomain { get; set; } = "";
        public List<FlashcatComponentData> Components { get; set; } = new();
        public List<FlashcatSectionData> Sections { get; set; } = new();
    }

    private sealed class FlashcatComponentData
    {
        [JsonPropertyName("component_id")] public string ComponentID { get; set; } = "";
        [JsonPropertyName("section_id")] public string? SectionID { get; set; }
        public string Name { get; set; } = "";
        public string? Status { get; set; }
    }

    private sealed class FlashcatSectionData
    {
        [JsonPropertyName("section_id")] public string SectionID { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private sealed class FlashcatChangeData
    {
        [JsonPropertyName("change_id")] public long ChangeID { get; set; }
        public string Title { get; set; } = "";
        public string Status { get; set; } = "";
        public string Type { get; set; } = "";
        [JsonPropertyName("affected_components")] public List<FlashcatComponentData> AffectedComponents { get; set; } = new();
    }
}
