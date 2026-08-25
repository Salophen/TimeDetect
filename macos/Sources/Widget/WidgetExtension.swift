import SwiftUI
import WidgetKit

struct TimeDetectWidgetEntry: TimelineEntry {
    let date: Date
    let snapshot: PhaseSnapshot
}

struct TimeDetectWidgetProvider: TimelineProvider {
    func placeholder(in context: Context) -> TimeDetectWidgetEntry {
        TimeDetectWidgetEntry(date: Date(), snapshot: PeakEngine.snapshot(at: Date()))
    }

    func getSnapshot(in context: Context, completion: @escaping (TimeDetectWidgetEntry) -> Void) {
        let now = Date()
        completion(TimeDetectWidgetEntry(date: now, snapshot: PeakEngine.snapshot(at: now)))
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<TimeDetectWidgetEntry>) -> Void) {
        let now = Date()
        let entries = WidgetTimelinePlan.entries(from: now, days: 3).map {
            TimeDetectWidgetEntry(date: $0.date, snapshot: $0.snapshot)
        }
        completion(Timeline(entries: entries, policy: .atEnd))
    }
}

struct TimeDetectWidgetView: View {
    let entry: TimeDetectWidgetEntry
    @Environment(\.widgetFamily) private var family

    var body: some View {
        Group {
            if family == .systemMedium {
                MediumPhaseCard(snapshot: entry.snapshot, usesSystemTimer: true)
            } else {
                SmallPhaseCard(snapshot: entry.snapshot, usesSystemTimer: true)
            }
        }
            .environment(\.timeZone, PeakEngine.beijingTimeZone)
            .containerBackground(for: .widget) {
                Color(red: 0.04, green: 0.04, blue: 0.06)
            }
    }
}

@main
struct TimeDetectWidget: Widget {
    let kind = "TimeDetectWidget"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: TimeDetectWidgetProvider()) { entry in
            TimeDetectWidgetView(entry: entry)
        }
        .configurationDisplayName("DeepSeek 峰谷时段")
        .description("北京时间实时显示 DeepSeek 峰时与谷时价格状态。")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}