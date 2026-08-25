using System.Linq;
using TimeDetect.Net;
using Xunit;

namespace TimeDetect.Windows.Tests;

public class StatusParserTests
{
    [Fact]
    public void OverallIndicatorMaps()
    {
        Assert.Equal(ServiceHealth.Operational, ServiceHealthExtensions.OverallIndicator("none"));
        Assert.Equal(ServiceHealth.Degraded, ServiceHealthExtensions.OverallIndicator("minor"));
        Assert.Equal(ServiceHealth.PartialOutage, ServiceHealthExtensions.OverallIndicator("major"));
        Assert.Equal(ServiceHealth.MajorOutage, ServiceHealthExtensions.OverallIndicator("critical"));
        Assert.Equal(ServiceHealth.Unknown, ServiceHealthExtensions.OverallIndicator("future"));
    }

    [Fact]
    public void ComponentStatusMaps()
    {
        Assert.Equal(ServiceHealth.Operational, ServiceHealthExtensions.ComponentStatus("operational"));
        Assert.Equal(ServiceHealth.Degraded, ServiceHealthExtensions.ComponentStatus("degraded_performance"));
        Assert.Equal(ServiceHealth.PartialOutage, ServiceHealthExtensions.ComponentStatus("partial_outage"));
        Assert.Equal(ServiceHealth.MajorOutage, ServiceHealthExtensions.ComponentStatus("major_outage"));
        Assert.Equal(ServiceHealth.Maintenance, ServiceHealthExtensions.ComponentStatus("under_maintenance"));
        Assert.Equal(ServiceHealth.Unknown, ServiceHealthExtensions.ComponentStatus("future"));
    }

    [Fact]
    public void MatchesComponentNames()
    {
        Assert.True(StatuspageParser.Matches("API Service", MonitoredServiceKind.Api));
        Assert.True(StatuspageParser.Matches("Web Chat Service", MonitoredServiceKind.WebChat));
    }

    [Fact]
    public void SummaryParsesOverallAndServices()
    {
        const string statusJSON =
            "{\"status\":{\"indicator\":\"minor\"},\"components\":[" +
            "{\"name\":\"API Service\",\"status\":\"degraded_performance\"}," +
            "{\"name\":\"Web Chat Service\",\"status\":\"operational\"}]," +
            "\"incidents\":[{\"id\":\"i1\",\"name\":\"API performance issue\",\"status\":\"investigating\"}]}";

        var snapshot = StatuspageParser.SummaryFrom(TestData.Json(statusJSON));

        Assert.Equal(ServiceHealth.Degraded, snapshot.Overall);
        Assert.Equal(ServiceHealth.Degraded, Find(snapshot, MonitoredServiceKind.Api).Health);
        Assert.Equal(ServiceHealth.Operational, Find(snapshot, MonitoredServiceKind.WebChat).Health);
        Assert.Equal("正在调查", snapshot.Incidents[0].StatusText);
    }

    [Fact]
    public void FlashcatPageParsesAndAggregates()
    {
        const string flashcatIncident =
            "[{\"change_id\":123,\"title\":\"API degraded\",\"status\":\"investigating\",\"type\":\"incident\"," +
            "\"affected_components\":[{\"component_id\":\"api-pro\",\"name\":\"API Service\",\"status\":\"full_outage\"}]}]";

        var snapshot = FlashcatParser.FlashcatPageFrom(TestData.FlashcatPage(
            apiStatus: "degraded", chatStatus: "partial_outage", activeChanges: flashcatIncident));

        Assert.Equal(ServiceHealth.MajorOutage, snapshot.Overall);
        Assert.Equal(ServiceHealth.MajorOutage, Find(snapshot, MonitoredServiceKind.Api).Health);
        Assert.Equal(ServiceHealth.PartialOutage, Find(snapshot, MonitoredServiceKind.WebChat).Health);
        Assert.Equal("正在调查", snapshot.Incidents[0].StatusText);
    }

    [Fact]
    public void FlashcatValidatesTenantIdentity()
    {
        Assert.Throws<FlashcatParseException>(() =>
            FlashcatParser.FlashcatPageFrom(TestData.FlashcatPage(pageId: 1)));
    }

    [Fact]
    public void PlainHtmlIsNotOperational()
    {
        Assert.Throws<FlashcatParseException>(() =>
            FlashcatParser.FlashcatPageFrom(TestData.Json("<html>Everything is running smoothly</html>")));
    }

    private static MonitoredService Find(ServiceStatusSnapshot snapshot, MonitoredServiceKind kind)
        => snapshot.Services.First(s => s.Kind == kind);
}
