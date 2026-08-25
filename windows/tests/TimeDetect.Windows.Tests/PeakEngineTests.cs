using System;
using System.Linq;
using TimeDetect.Core;
using Xunit;

namespace TimeDetect.Windows.Tests;

public class PeakEngineTests
{
    // 2026-08-20 为周四（对应 macOS 测试的基准日）。
    private static readonly DateTimeOffset BaseDay =
        new(2026, 8, 20, 0, 0, 0, TimeSpan.FromHours(8));

    private static DateTimeOffset BeijingDate(int hour, int minute, int second = 0, int dayOffset = 0)
        => BaseDay.AddDays(dayOffset).AddHours(hour).AddMinutes(minute).AddSeconds(second);

    [Fact]
    public void PhaseBoundariesFollowBeijingRules()
    {
        Assert.Equal(PricePhase.OffPeak, PeakEngine.PhaseAt(BeijingDate(8, 59, 59)));
        Assert.Equal(PricePhase.Peak, PeakEngine.PhaseAt(BeijingDate(9, 0)));
        Assert.Equal(PricePhase.Peak, PeakEngine.PhaseAt(BeijingDate(11, 59, 59)));
        Assert.Equal(PricePhase.OffPeak, PeakEngine.PhaseAt(BeijingDate(12, 0)));
        Assert.Equal(PricePhase.OffPeak, PeakEngine.PhaseAt(BeijingDate(13, 59, 59)));
        Assert.Equal(PricePhase.Peak, PeakEngine.PhaseAt(BeijingDate(14, 0)));
        Assert.Equal(PricePhase.Peak, PeakEngine.PhaseAt(BeijingDate(17, 59, 59)));
        Assert.Equal(PricePhase.OffPeak, PeakEngine.PhaseAt(BeijingDate(18, 0)));
    }

    [Fact]
    public void WeekendsAreEntirelyOffPeak()
    {
        var saturday = BeijingDate(9, 0, dayOffset: 2);
        var sunday = BeijingDate(14, 0, dayOffset: 3);
        var monday = BeijingDate(9, 0, dayOffset: 4);

        Assert.Equal(PricePhase.OffPeak, PeakEngine.PhaseAt(saturday));
        Assert.Equal(PricePhase.OffPeak, PeakEngine.PhaseAt(sunday));
        Assert.Equal(monday, PeakEngine.NextBoundaryAfter(saturday));
        Assert.Equal(monday, PeakEngine.NextBoundaryAfter(sunday));
    }

    [Fact]
    public void EveningTransitionsToNextMorningAtNine()
    {
        var evening = BeijingDate(23, 30);
        var nextMorning = BeijingDate(9, 0, dayOffset: 1);

        Assert.Equal(nextMorning, PeakEngine.NextBoundaryAfter(evening));
        Assert.Equal(34200.0, PeakEngine.SnapshotAt(evening).SecondsToNextBoundary, 3);
    }

    [Fact]
    public void FridayEveningTransitionsToMondayMorning()
    {
        var fridayEvening = BeijingDate(19, 0, dayOffset: 1);
        var monday = BeijingDate(9, 0, dayOffset: 4);

        Assert.Equal(monday, PeakEngine.NextBoundaryAfter(fridayEvening));
    }

    [Fact]
    public void CountdownTextFormatsHoursAndMinutes()
    {
        Assert.Equal("1:01:01", PeakEngine.CountdownText(3661));
        Assert.Equal("00:59", PeakEngine.CountdownText(59));
    }

    [Fact]
    public void WidgetEntriesContainOnlyDailyTransitions()
    {
        var entries = WidgetTimelinePlan.EntriesFrom(BeijingDate(8, 0), days: 1);
        Assert.Equal(
            new[]
            {
                BeijingDate(8, 0), BeijingDate(9, 0), BeijingDate(12, 0),
                BeijingDate(14, 0), BeijingDate(18, 0)
            },
            entries.Select(e => e.Date));
    }

    [Fact]
    public void WeekendWidgetHasNoFalsePeakTransitions()
    {
        var saturday = BeijingDate(9, 0, dayOffset: 2);
        var entries = WidgetTimelinePlan.EntriesFrom(saturday, days: 1);

        Assert.Single(entries);
        Assert.Equal(saturday, entries[0].Date);
    }
}
