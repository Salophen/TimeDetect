import Foundation

/// 一个重复的北京时间通知计划。仅包含纯 Foundation 数据，便于单元测试。
struct NotificationPlan: Equatable {
    let identifier: String
    /// UNCalendarNotificationTrigger 的 weekday：周日为 1，周一为 2，依次到周六为 7。
    let weekday: Int
    let hour: Int
    let minute: Int
    let title: String
    let body: String

    var dateComponents: DateComponents {
        var components = DateComponents()
        components.timeZone = PeakEngine.beijingTimeZone
        components.weekday = weekday
        components.hour = hour
        components.minute = minute
        return components
    }
}

enum TimeDetectNotificationPlan {
    static let offPeakStartMinutes = [12 * 60, 18 * 60]
    static let weekdays = Array(2...6)

    static func offPeakPlans() -> [NotificationPlan] {
        weekdays.flatMap { weekday in
            offPeakStartMinutes.map { minute in
                let end = minute == 12 * 60 ? "14:00" : "次日 09:00"
                return NotificationPlan(
                    identifier: identifier(for: minute, weekday: weekday, kind: "offpeak"),
                    weekday: weekday,
                    hour: minute / 60,
                    minute: minute % 60,
                    title: "梁文谷上线",
                    body: "当前已进入谷时 · 0.5x 半价，本轮谷时持续至\(end)。"
                )
            }
        }
    }

    static func advancePlans(minutes advanceMinutes: Int) -> [NotificationPlan] {
        let safeMinutes = max(0, advanceMinutes)
        return weekdays.flatMap { weekday in
            offPeakStartMinutes.map { start in
                let notificationMinute = start - safeMinutes
                let normalizedMinute = (notificationMinute + 1440) % 1440
                return NotificationPlan(
                    identifier: identifier(for: start, weekday: weekday, kind: "advance"),
                    weekday: weekday,
                    hour: normalizedMinute / 60,
                    minute: normalizedMinute % 60,
                    title: "DeepSeek 即将进入谷时",
                    body: "距离谷时还有 \(safeMinutes) 分钟，\(clockText(for: start)) 起进入 0.5x 半价。"
                )
            }
        }
    }

    private static func identifier(for minute: Int, weekday: Int, kind: String) -> String {
        String(format: "timedetect.%@.%d.%02d%02d", kind, weekday, minute / 60, minute % 60)
    }

    private static func clockText(for minute: Int) -> String {
        String(format: "%02d:%02d", minute / 60, minute % 60)
    }
}