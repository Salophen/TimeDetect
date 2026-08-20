import SwiftUI
import Combine

/// 全局状态源。低功耗设计要点：
///   1. 整个 App 只有一个 1s 定时器，配 tolerance 让系统合并唤醒；
///   2. 秒针只驱动一个轻量 @Published，视图 diff 范围极小；
///   3. 峰谷判定是纯函数，不做任何 I/O、不落盘、不联网。
@MainActor
final class PhaseStore: ObservableObject {

    @Published private(set) var snapshot: PhaseSnapshot = PeakEngine.snapshot()

    /// 桌面悬浮挂件是否可见。
    @Published var floatingWidgetVisible: Bool {
        didSet { defaults.set(floatingWidgetVisible, forKey: Keys.floatingVisible) }
    }

    private enum Keys {
        static let floatingVisible = "floatingWidgetVisible"
    }

    private let defaults = UserDefaults.standard
    private var timer: Timer?

    init() {
        // 首次启动默认显示桌面挂件
        defaults.register(defaults: [Keys.floatingVisible: true])
        floatingWidgetVisible = defaults.bool(forKey: Keys.floatingVisible)
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
