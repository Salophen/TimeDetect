using System;
using System.Collections.Generic;

namespace TimeDetect.Core;

/// <summary>桌面挂件与 App 共用的纯数据层。</summary>
public readonly record struct WidgetPhaseEntry(DateTimeOffset Date, PhaseSnapshot Snapshot);

public static class WidgetTimelinePlan
{
    /// <summary>从当前时间开始，预排未来 days 天的所有状态切换。</summary>
    public static List<WidgetPhaseEntry> EntriesFrom(DateTimeOffset date, int days = 2)
    {
        var points = new List<DateTimeOffset> { date };
        points.AddRange(PeakEngine.BoundariesCovering(date, days));
        points.Sort();
        var result = new List<WidgetPhaseEntry>();
        foreach (var point in points)
        {
            if (result.Count > 0 && result[^1].Date == point) continue;
            result.Add(new WidgetPhaseEntry(point, PeakEngine.SnapshotAt(point)));
        }
        return result;
    }
}
