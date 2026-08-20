import Foundation

/// Widget 与 App 共用的纯数据层，避免 Widget 扩展自行复制峰谷规则。
struct WidgetPhaseEntry: Identifiable {
    let date: Date
    let snapshot: PhaseSnapshot

    var id: Date { date }
}

enum WidgetTimelinePlan {
    /// 从当前时间开始，预排未来两天的所有状态切换。
    /// 每个 entry 的 snapshot 固定在切换点，状态不会依赖扩展后台常驻。
    static func entries(from date: Date, days: Int = 2) -> [WidgetPhaseEntry] {
        let points = [date] + PeakEngine.boundaries(coveringDaysFrom: date, days: days)
        return points.sorted().reduce(into: [WidgetPhaseEntry]()) { result, point in
            guard result.last?.date != point else { return }
            result.append(WidgetPhaseEntry(date: point, snapshot: PeakEngine.snapshot(at: point)))
        }
    }
}