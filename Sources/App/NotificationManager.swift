import Foundation
@preconcurrency import UserNotifications

@MainActor
final class NotificationManager: ObservableObject {
    enum PermissionState: Equatable {
        case authorized
        case denied
        case notDetermined
        case provisional
        case unavailable

        var isUsable: Bool { self == .authorized || self == .provisional }
    }

    @Published private(set) var permissionState: PermissionState = .notDetermined
    private let center = UNUserNotificationCenter.current()

    func refreshPermissionState() {
        center.getNotificationSettings { [weak self] settings in
            Task { @MainActor in
                self?.permissionState = Self.map(settings.authorizationStatus)
            }
        }
    }

    func requestPermissionAndSchedule(
        offPeakEnabled: Bool,
        advanceEnabled: Bool,
        advanceMinutes: Int
    ) {
        let center = center
        center.getNotificationSettings { [weak self] settings in
            guard let self else { return }
            switch settings.authorizationStatus {
            case .notDetermined:
                center.requestAuthorization(options: [.alert, .sound]) { granted, _ in
                    Task { @MainActor in
                        self.permissionState = granted ? .authorized : .denied
                        self.scheduleIfPossible(
                            offPeakEnabled: offPeakEnabled,
                            advanceEnabled: advanceEnabled,
                            advanceMinutes: advanceMinutes
                        )
                    }
                }
            default:
                Task { @MainActor in
                    self.permissionState = Self.map(settings.authorizationStatus)
                    self.scheduleIfPossible(
                        offPeakEnabled: offPeakEnabled,
                        advanceEnabled: advanceEnabled,
                        advanceMinutes: advanceMinutes
                    )
                }
            }
        }
    }

    func reschedule(offPeakEnabled: Bool, advanceEnabled: Bool, advanceMinutes: Int) {
        center.getNotificationSettings { [weak self] settings in
            Task { @MainActor in
                guard let self else { return }
                self.permissionState = Self.map(settings.authorizationStatus)
                self.scheduleIfPossible(
                    offPeakEnabled: offPeakEnabled,
                    advanceEnabled: advanceEnabled,
                    advanceMinutes: advanceMinutes
                )
            }
        }
    }

    /// 提前提醒开关或分钟数变化时，只管理提前提醒请求，不触碰谷时开始请求。
    func rescheduleAdvance(offPeakEnabled: Bool, advanceEnabled: Bool, advanceMinutes: Int) {
        center.getNotificationSettings { [weak self] settings in
            Task { @MainActor in
                guard let self else { return }
                self.permissionState = Self.map(settings.authorizationStatus)
                self.center.removePendingNotificationRequests(withIdentifiers: Self.advanceIdentifiers)
                guard self.permissionState.isUsable, offPeakEnabled, advanceEnabled else { return }
                self.add(TimeDetectNotificationPlan.advancePlans(minutes: advanceMinutes))
            }
        }
    }

    private func scheduleIfPossible(
        offPeakEnabled: Bool,
        advanceEnabled: Bool,
        advanceMinutes: Int
    ) {
        let identifiers = Self.managedIdentifiers
        center.removePendingNotificationRequests(withIdentifiers: identifiers)
        guard permissionState.isUsable else { return }

        var plans: [NotificationPlan] = []
        if offPeakEnabled {
            plans += TimeDetectNotificationPlan.offPeakPlans()
            if advanceEnabled {
                plans += TimeDetectNotificationPlan.advancePlans(minutes: advanceMinutes)
            }
        }

        add(plans)
    }

    private func add(_ plans: [NotificationPlan]) {
        for plan in plans {
            let content = UNMutableNotificationContent()
            content.title = plan.title
            content.body = plan.body
            content.sound = .default
            let trigger = UNCalendarNotificationTrigger(dateMatching: plan.dateComponents, repeats: true)
            center.add(UNNotificationRequest(identifier: plan.identifier, content: content, trigger: trigger))
        }
    }

    private static let managedIdentifiers: [String] = {
        let offPeak = TimeDetectNotificationPlan.offPeakPlans().map(\.identifier)
        return offPeak + advanceIdentifiers
    }()

    private static let advanceIdentifiers = TimeDetectNotificationPlan
        .advancePlans(minutes: 5)
        .map(\.identifier)

    private static func map(_ status: UNAuthorizationStatus) -> PermissionState {
        switch status {
        case .authorized: return .authorized
        case .denied: return .denied
        case .provisional: return .provisional
        case .notDetermined: return .notDetermined
        @unknown default: return .unavailable
        }
    }
}