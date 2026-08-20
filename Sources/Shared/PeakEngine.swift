import Foundation

/// DeepSeek 计费时段。
/// 官方规则（北京时间 UTC+8）：高峰时段 09:00-12:00、14:00-18:00，其余为空闲时段。
/// 空闲时段价格为高峰时段的一半。
enum PricePhase: String, Hashable {
    case peak      // 高峰时段 —— 梁文锋
    case offPeak   // 空闲时段 —— 梁文谷

    /// 组件主角：峰时「梁文锋」，谷时「梁文谷」。
    var personaName: String {
        switch self {
        case .peak: return "梁文锋"
        case .offPeak: return "梁文谷"
        }
    }

    /// 时段短名，用于徽章。
    var shortLabel: String {
        switch self {
        case .peak: return "峰时"
        case .offPeak: return "谷时"
        }
    }

    /// 价格倍率文案，沿用 DeepSeek 规则中的倍率表达。
    var priceLabel: String {
        switch self {
        case .peak: return "1.0x 原价"
        case .offPeak: return "0.5x 半价"
        }
    }

    /// 小尺寸徽章使用的紧凑倍率。
    var multiplierLabel: String {
        switch self {
        case .peak: return "1.0x"
        case .offPeak: return "0.5x"
        }
    }

    var priceKindLabel: String {
        switch self {
        case .peak: return "原价"
        case .offPeak: return "半价"
        }
    }

    /// 英文副标题，用于排版层次。
    var latinLabel: String {
        switch self {
        case .peak: return "PEAK"
        case .offPeak: return "OFF-PEAK"
        }
    }

    var opposite: PricePhase {
        self == .peak ? .offPeak : .peak
    }
}

/// 一天之内的时段切片（以北京时间的「零点起分钟数」表示）。
struct PhaseWindow: Hashable {
    let startMinute: Int
    let endMinute: Int
    let phase: PricePhase

    var lengthInMinutes: Int { endMinute - startMinute }
}

/// 视图渲染所需的一次性快照，App 与 WidgetKit 共用。
struct PhaseSnapshot {
    let date: Date
    let phase: PricePhase
    /// 下一次峰谷切换的时刻。
    let nextBoundary: Date
    /// 北京时间当日已过分钟数（0..<1440）。
    let beijingMinuteOfDay: Int
    /// 当前处于北京时间一天中的进度（0...1），用于时间轴指针。
    let dayProgress: Double
    /// 当前所在时段还剩多少秒。
    let secondsToNextBoundary: TimeInterval
    /// 本机时区是否就是北京时间。
    let isLocalBeijing: Bool
}

/// 峰谷判定与时间轴计算。纯函数、无状态，便于低功耗的一次性求值。
enum PeakEngine {

    static let beijingTimeZone = TimeZone(identifier: "Asia/Shanghai") ?? TimeZone(secondsFromGMT: 8 * 3600)!

    /// 高峰时段区间（北京时间，单位：分钟）。
    static let peakRanges: [(start: Int, end: Int)] = [
        (9 * 60, 12 * 60),
        (14 * 60, 18 * 60)
    ]

    /// 一天内所有真正发生峰谷切换的节点。
    /// 24:00 只是当天结束，不是一次额外的切换；18:00 后的下一次切换是次日 09:00。
    static let boundaryMinutes: [Int] = [9 * 60, 12 * 60, 14 * 60, 18 * 60]

    /// 北京时间当日 24 小时完整分段，用于绘制时间轴。
    static let dayWindows: [PhaseWindow] = [
        PhaseWindow(startMinute: 0, endMinute: 9 * 60, phase: .offPeak),
        PhaseWindow(startMinute: 9 * 60, endMinute: 12 * 60, phase: .peak),
        PhaseWindow(startMinute: 12 * 60, endMinute: 14 * 60, phase: .offPeak),
        PhaseWindow(startMinute: 14 * 60, endMinute: 18 * 60, phase: .peak),
        PhaseWindow(startMinute: 18 * 60, endMinute: 24 * 60, phase: .offPeak)
    ]

    private static var beijingCalendar: Calendar = {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = beijingTimeZone
        return calendar
    }()

    /// 指定时刻所属的「北京时间当日零点」。
    static func startOfBeijingDay(for date: Date) -> Date {
        beijingCalendar.startOfDay(for: date)
    }

    /// 北京时间当日已过的分钟数（含小数）。
    static func beijingMinutes(for date: Date) -> Double {
        date.timeIntervalSince(startOfBeijingDay(for: date)) / 60
    }

    static func phase(at date: Date) -> PricePhase {
        let minutes = beijingMinutes(for: date)
        for range in peakRanges where minutes >= Double(range.start) && minutes < Double(range.end) {
            return .peak
        }
        return .offPeak
    }

    /// 下一次峰谷切换时刻。
    static func nextBoundary(after date: Date) -> Date {
        let dayStart = startOfBeijingDay(for: date)
        let minutes = beijingMinutes(for: date)
        for boundary in boundaryMinutes where Double(boundary) > minutes {
            return dayStart.addingTimeInterval(Double(boundary) * 60)
        }
        // 18:00 之后直到次日 09:00 都是谷时。
        return dayStart.addingTimeInterval(24 * 3600 + 9 * 60 * 60)
    }

    /// 当天全部切换时刻，供 WidgetKit 预排 Timeline（低功耗关键）。
    static func boundaries(coveringDaysFrom date: Date, days: Int = 2) -> [Date] {
        let dayStart = startOfBeijingDay(for: date)
        var result: [Date] = []
        guard days > 0 else { return [] }
        for dayOffset in 0..<days {
            let base = beijingCalendar.date(byAdding: .day, value: dayOffset, to: dayStart) ?? dayStart
            for boundary in boundaryMinutes {
                let candidate = base.addingTimeInterval(Double(boundary) * 60)
                if candidate > date { result.append(candidate) }
            }
        }
        return result.sorted()
    }

    static func snapshot(at date: Date = Date()) -> PhaseSnapshot {
        let minutes = beijingMinutes(for: date)
        let boundary = nextBoundary(after: date)
        return PhaseSnapshot(
            date: date,
            phase: phase(at: date),
            nextBoundary: boundary,
            beijingMinuteOfDay: Int(minutes),
            dayProgress: min(max(minutes / 1440, 0), 1),
            secondsToNextBoundary: max(boundary.timeIntervalSince(date), 0),
            isLocalBeijing: TimeZone.current.secondsFromGMT(for: date) == beijingTimeZone.secondsFromGMT(for: date)
        )
    }

    /// 把秒数格式化为 `H:mm:ss` 倒计时。
    static func countdownText(_ seconds: TimeInterval) -> String {
        let total = Int(seconds.rounded(.down))
        let hours = total / 3600
        let minutes = (total % 3600) / 60
        let secs = total % 60
        if hours > 0 {
            return String(format: "%d:%02d:%02d", hours, minutes, secs)
        }
        return String(format: "%02d:%02d", minutes, secs)
    }

    /// 把北京时间分钟数格式化为 `HH:mm`。
    static func clockText(fromBeijingMinute minute: Int) -> String {
        String(format: "%02d:%02d", (minute / 60) % 24, minute % 60)
    }
}
