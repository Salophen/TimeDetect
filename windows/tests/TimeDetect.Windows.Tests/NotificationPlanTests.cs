using System.Linq;
using TimeDetect.Core;
using Xunit;

namespace TimeDetect.Windows.Tests;

public class NotificationPlanTests
{
    [Fact]
    public void OffPeakPlansCoverFiveWeekdays()
    {
        var plans = TimeDetectNotificationPlan.OffPeakPlans();

        Assert.Equal(10, plans.Count);
        Assert.Equal(new[] { 2, 3, 4, 5, 6 },
            plans.Select(p => p.Weekday).Distinct().OrderBy(w => w));

        var mondayPlans = plans.Where(p => p.Weekday == 2).ToList();
        Assert.Equal(2, mondayPlans.Count);
        Assert.Equal(12, mondayPlans[0].Hour);
        Assert.Equal(0, mondayPlans[0].Minute);
        Assert.Equal(18, mondayPlans[1].Hour);
        Assert.Equal(0, mondayPlans[1].Minute);

        Assert.Equal("timedetect.offpeak.2.1200", plans[0].Identifier);
        Assert.Equal("timedetect.offpeak.2.1800", plans[1].Identifier);
    }

    [Fact]
    public void AdvancePlansMatchExpectedTimes()
    {
        foreach (var minutes in new[] { 5, 10, 15, 30 })
        {
            var plans = TimeDetectNotificationPlan.AdvancePlans(minutes)
                .Where(p => p.Weekday == 2)
                .ToList();

            Assert.Equal(2, plans.Count);
            Assert.Equal((12 * 60 - minutes) / 60, plans[0].Hour);
            Assert.Equal((12 * 60 - minutes) % 60, plans[0].Minute);
            Assert.Equal((18 * 60 - minutes) / 60, plans[1].Hour);
            Assert.Equal((18 * 60 - minutes) % 60, plans[1].Minute);
        }
    }

    [Fact]
    public void AdvanceIdentifiersAreStableAcrossMinuteSettings()
    {
        var plans = TimeDetectNotificationPlan.AdvancePlans(10)
            .Where(p => p.Weekday == 2)
            .Select(p => p.Identifier)
            .ToList();

        Assert.Equal(new[] { "timedetect.advance.2.1200", "timedetect.advance.2.1800" }, plans);
    }

    [Fact]
    public void FloatingWindowModeDefaultsAndParses()
    {
        Assert.Equal(FloatingWindowMode.Desktop, FloatingWindowModeExtensions.DefaultMode);
        Assert.Equal(FloatingWindowMode.Desktop, FloatingWindowModeExtensions.FromStored(null));
        Assert.Equal(FloatingWindowMode.Desktop, FloatingWindowModeExtensions.FromStored("invalid"));
        Assert.Equal(FloatingWindowMode.AlwaysOnTop, FloatingWindowModeExtensions.FromStored("alwaysOnTop"));
        Assert.Equal("始终置顶", FloatingWindowModeExtensions.Title(FloatingWindowMode.AlwaysOnTop));
        Assert.Equal("桌面模式", FloatingWindowModeExtensions.Title(FloatingWindowMode.Desktop));
    }
}
