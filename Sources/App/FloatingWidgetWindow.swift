import SwiftUI
import AppKit

/// 桌面悬浮挂件窗口。
///
/// 关键设置：
///   - `.borderless` + 透明背景：只看到卡片本身，没有标题栏；
///   - `level = .normal - 1`：贴在桌面层，不会盖住正常工作窗口；
///   - `collectionBehavior` 含 `.canJoinAllSpaces` + `.stationary`：切桌面时不闪；
///   - 不进入 Dock、不抢焦点，点击拖动即可移动位置。
final class FloatingWidgetWindow: NSPanel {

    private static let defaultSize = NSSize(width: 240, height: 240)

    init(
        store: PhaseStore,
        statusManager: DeepSeekStatusManager,
        balanceManager: DeepSeekBalanceManager
    ) {
        super.init(
            contentRect: NSRect(origin: .zero, size: Self.defaultSize),
            styleMask: [.borderless, .nonactivatingPanel, .resizable],
            backing: .buffered,
            defer: false
        )

        isFloatingPanel = true
        applyWindowMode(store.floatingWindowMode)
        collectionBehavior = [.canJoinAllSpaces, .stationary, .ignoresCycle]
        isOpaque = false
        backgroundColor = .clear
        hasShadow = true
        isMovableByWindowBackground = true
        hidesOnDeactivate = false
        animationBehavior = .utilityWindow
        // 不出现在窗口列表 / Mission Control 缩略图里
        isExcludedFromWindowsMenu = true

        let root = FloatingWidgetView()
            .environmentObject(store)
            .environmentObject(statusManager)
            .environmentObject(balanceManager)

        contentView = NSHostingView(rootView: root)
        minSize = NSSize(width: 220, height: 220)
        setContentSize(Self.defaultSize)
        positionAtDefaultCornerIfNeeded()
    }

    /// 只调整窗口层级，不重建窗口，也不改变用户保存的尺寸与坐标。
    func applyWindowMode(_ mode: FloatingWindowMode) {
        switch mode {
        case .desktop:
            level = NSWindow.Level(rawValue: NSWindow.Level.normal.rawValue - 1)
        case .alwaysOnTop:
            level = .floating
        }
    }

    /// borderless 窗口默认不能成为 key window，这里放开以便接收右键菜单。
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }

    /// 首次启动时放到主屏右上角，之后由系统记住用户拖动的位置。
    private func positionAtDefaultCornerIfNeeded() {
        setFrameAutosaveName("TimeDetectFloatingWidget")
        if frame.origin == .zero, let screen = NSScreen.main {
            let visible = screen.visibleFrame
            let origin = NSPoint(
                x: visible.maxX - frame.width - 24,
                y: visible.maxY - frame.height - 24
            )
            setFrameOrigin(origin)
        }

        // 兼容旧版保存的 190 × 190 frame：保留其右上角位置，只升级尺寸。
        if frame.width < Self.defaultSize.width || frame.height < Self.defaultSize.height {
            let topRight = NSPoint(x: frame.maxX, y: frame.maxY)
            let upgradedFrame = NSRect(
                x: topRight.x - Self.defaultSize.width,
                y: topRight.y - Self.defaultSize.height,
                width: Self.defaultSize.width,
                height: Self.defaultSize.height
            )
            setFrame(upgradedFrame, display: false)
        }
    }
}

/// 悬浮挂件内容：复用 SmallPhaseCard，加一层拖动/关闭的交互装饰。
struct FloatingWidgetView: View {
    @EnvironmentObject private var store: PhaseStore
    @EnvironmentObject private var statusManager: DeepSeekStatusManager
    @EnvironmentObject private var balanceManager: DeepSeekBalanceManager
    @State private var isHovering = false

    var body: some View {
        SmallPhaseCard(
            snapshot: store.snapshot,
            accessory: AnyView(DeepSeekOverview())
        )
            .overlay(alignment: .topLeading) {
                if isHovering {
                    Button {
                        store.floatingWidgetVisible = false
                    } label: {
                        Image(systemName: "xmark")
                            .font(.system(size: 8, weight: .bold))
                            .foregroundStyle(Color.white.opacity(0.75))
                            .frame(width: 16, height: 16)
                            .background(Circle().fill(Color.black.opacity(0.45)))
                    }
                    .buttonStyle(.plain)
                    .padding(6)
                    .help("隐藏挂件（可从菜单栏重新打开）")
                    .accessibilityLabel("隐藏桌面挂件")
                }
            }
            .onHover { isHovering = $0 }
            // 让整卡都能拖动
            .contentShape(Rectangle())
            .animation(.easeInOut(duration: 0.18), value: isHovering)
            .animation(.easeInOut(duration: 0.45), value: store.snapshot.phase)
    }
}

/// 悬浮窗中的最小信息摘要：余额更醒目，服务状态作为辅助信息。
private struct DeepSeekOverview: View {
    @EnvironmentObject private var statusManager: DeepSeekStatusManager
    @EnvironmentObject private var balanceManager: DeepSeekBalanceManager

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 8) {
            Text(balanceText)
                .font(.system(size: 15, weight: .semibold, design: .rounded))
                .monospacedDigit()
                .foregroundStyle(Color.white.opacity(0.92))
                .lineLimit(1)
                .minimumScaleFactor(0.75)

            Spacer(minLength: 2)

            HStack(spacing: 4) {
                Circle()
                    .fill(statusColor)
                    .frame(width: 5, height: 5)
                Text(statusText)
                    .lineLimit(1)
            }
            .font(.system(size: 9, weight: .medium, design: .rounded))
            .foregroundStyle(Color.white.opacity(0.58))
        }
        .accessibilityElement(children: .combine)
    }

    private var balanceText: String {
        guard let snapshot = balanceManager.balance,
              let balance = snapshot.balances.first(where: { $0.currency == "CNY" })
                ?? snapshot.balances.first else {
            return balanceManager.isRefreshing ? "余额 …" : "余额 —"
        }
        return "余额 \(balance.totalText)"
    }

    private var statusText: String {
        if let health = statusManager.snapshot?.overall {
            return "DeepSeek \(health.title)"
        }
        return statusManager.isRefreshing ? "DeepSeek 检测中" : "DeepSeek 状态未知"
    }

    private var statusColor: Color {
        guard let health = statusManager.snapshot?.overall else {
            return statusManager.isRefreshing ? .blue : Color.white.opacity(0.3)
        }
        switch health {
        case .operational: return .green
        case .maintenance, .degraded: return .yellow
        case .partialOutage, .majorOutage: return .red
        case .unknown: return Color.white.opacity(0.3)
        }
    }
}
