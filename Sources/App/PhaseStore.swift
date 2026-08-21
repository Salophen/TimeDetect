import SwiftUI
import Combine

/// 全局状态源。低功耗设计要点：
///   1. 峰谷时钟只有一个 1s 本地定时器，配 tolerance 让系统合并唤醒；
///   2. 秒针只驱动一个轻量 @Published，视图 diff 范围极小；
///   3. 峰谷判定是纯函数，不做任何 I/O；独立网络 Manager 使用低频 async polling。
@MainActor
final class PhaseStore: ObservableObject {

    @Published private(set) var snapshot: PhaseSnapshot = PeakEngine.snapshot()

    /// 桌面悬浮挂件是否可见。
    @Published var floatingWidgetVisible: Bool {
        didSet { defaults.set(floatingWidgetVisible, forKey: Keys.floatingVisible) }
    }

    @Published var floatingWindowMode: FloatingWindowMode {
        didSet { defaults.set(floatingWindowMode.rawValue, forKey: Keys.floatingWindowMode) }
    }

    @Published var offPeakNotificationEnabled: Bool {
        didSet { defaults.set(offPeakNotificationEnabled, forKey: Keys.offPeakNotificationEnabled) }
    }

    @Published var advanceNotificationEnabled: Bool {
        didSet { defaults.set(advanceNotificationEnabled, forKey: Keys.advanceNotificationEnabled) }
    }

    @Published var advanceNotificationMinutes: Int {
        didSet { defaults.set(advanceNotificationMinutes, forKey: Keys.advanceNotificationMinutes) }
    }

    private enum Keys {
        static let floatingVisible = "floatingWidgetVisible"
        static let floatingWindowMode = "floatingWindowMode"
        static let offPeakNotificationEnabled = "offPeakNotificationEnabled"
        static let advanceNotificationEnabled = "advanceNotificationEnabled"
        static let advanceNotificationMinutes = "advanceNotificationMinutes"
    }

    private let defaults = UserDefaults.standard
    private var timer: Timer?

    init() {
        // 首次启动默认显示桌面挂件
        defaults.register(defaults: [
            Keys.floatingVisible: true,
            Keys.floatingWindowMode: FloatingWindowMode.defaultMode.rawValue,
            Keys.offPeakNotificationEnabled: false,
            Keys.advanceNotificationEnabled: false,
            Keys.advanceNotificationMinutes: 10
        ])
        floatingWidgetVisible = defaults.bool(forKey: Keys.floatingVisible)
        floatingWindowMode = FloatingWindowMode(
            storedRawValue: defaults.string(forKey: Keys.floatingWindowMode)
        )
        offPeakNotificationEnabled = defaults.bool(forKey: Keys.offPeakNotificationEnabled)
        advanceNotificationEnabled = defaults.bool(forKey: Keys.advanceNotificationEnabled)
        let savedAdvanceMinutes = defaults.integer(forKey: Keys.advanceNotificationMinutes)
        advanceNotificationMinutes = [5, 10, 15, 30].contains(savedAdvanceMinutes)
            ? savedAdvanceMinutes
            : 10
        snapshot = PeakEngine.snapshot()
    }

    func start() {
        guard timer == nil else { return }
        let timer = Timer(timeInterval: 1.0, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.tick() }
        }
        // 容差让 CPU 可以批量唤醒，比精确定时更省电
        timer.tolerance = 0.25
        RunLoop.main.add(timer, forMode: .common)
        self.timer = timer
    }

    func stop() {
        timer?.invalidate()
        timer = nil
    }

    private func tick() {
        snapshot = PeakEngine.snapshot()
    }
}
