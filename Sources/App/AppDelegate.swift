import SwiftUI
import AppKit
import Combine
import QuartzCore

/// 应用主控。职责：
    ///   - 建立菜单栏 NSStatusItem（仅显示常驻图标）
///   - 管理桌面悬浮挂件窗口的显示 / 隐藏
///   - 在峰谷切换瞬间给一次轻量提示
///
/// 用 NSApplicationDelegate 而非 SwiftUI `MenuBarExtra`，
/// 因为要同时控制一个自定义层级的 NSPanel，并兼容纯 swiftc 构建。
@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {

    let store = PhaseStore()

    private var statusItem: NSStatusItem?
    private var popover: NSPopover?
    private var floatingWindow: FloatingWidgetWindow?
    private var cancellables: Set<AnyCancellable> = []
    /// 上一次渲染的时段，用于只在切换瞬间做提示。
    private var lastPhase: PricePhase = PeakEngine.snapshot().phase

    func applicationDidFinishLaunching(_ notification: Notification) {
        // 纯菜单栏应用：不进 Dock、不显示主窗口
        NSApp.setActivationPolicy(.accessory)

        // Finder 双击启动时始终给出可见反馈；即使上次手动隐藏过挂件，
        // 新一次启动也会重新显示。运行期间仍可从挂件或菜单栏隐藏。
        store.floatingWidgetVisible = true
        setUpStatusItem()
        setUpPopover()
        bindStore()
        store.start()
        syncFloatingWindow()
    }

    /// App 已在运行时再次从 Finder 双击，会收到 reopen 而不是创建第二个进程。
    /// 此时重新显示挂件，并把它带到当前桌面可见层级。
    func applicationShouldHandleReopen(
        _ sender: NSApplication,
        hasVisibleWindows flag: Bool
    ) -> Bool {
        store.floatingWidgetVisible = true
        syncFloatingWindow(bringForward: true)
        return true
    }

    func applicationWillTerminate(_ notification: Notification) {
        store.stop()
        cancellables.removeAll()
    }

    // MARK: - 菜单栏

    private func setUpStatusItem() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        item.button?.target = self
        item.button?.action = #selector(handleStatusItemClick(_:))
        item.button?.sendAction(on: [.leftMouseUp, .rightMouseUp])
        statusItem = item
        configureStatusItemIcon()
    }

    /// 只显示 SF Symbol 模板图标，自动适应菜单栏深浅色；不再逐秒更新文字。
    private func configureStatusItemIcon() {
        guard let button = statusItem?.button else { return }
        let configuration = NSImage.SymbolConfiguration(pointSize: 14, weight: .semibold)
        let image = NSImage(systemSymbolName: "waveform.path.ecg", accessibilityDescription: "TimeDetect 运行中")?
            .withSymbolConfiguration(configuration)
        image?.isTemplate = true
        button.image = image
        button.imagePosition = .imageOnly
        button.imageScaling = .scaleProportionallyDown
        button.title = ""
        button.toolTip = "TimeDetect 正在运行"
        button.setAccessibilityLabel("TimeDetect 运行中")
    }

    @objc private func handleStatusItemClick(_ sender: NSStatusBarButton) {
        let isRightClick = NSApp.currentEvent?.type == .rightMouseUp
        if isRightClick {
            showContextMenu(from: sender)
        } else {
            togglePopover(from: sender)
        }
    }

    // MARK: - 弹出面板

    private func setUpPopover() {
        let popover = NSPopover()
        popover.behavior = .transient
        popover.animates = true
        popover.contentSize = NSSize(width: 368, height: 320)
        popover.contentViewController = NSHostingController(
            rootView: MenuBarPanel().environmentObject(store)
        )
        self.popover = popover
    }

    private func togglePopover(from sender: NSStatusBarButton) {
        guard let popover else { return }
        if popover.isShown {
            popover.performClose(nil)
        } else {
            popover.show(relativeTo: sender.bounds, of: sender, preferredEdge: .minY)
            // 让面板里的 Toggle 能直接响应点击
            popover.contentViewController?.view.window?.makeKey()
        }
    }

    /// 右键菜单：快速开关 + 退出。
    private func showContextMenu(from sender: NSStatusBarButton) {
        let menu = NSMenu()

        let toggleWidget = NSMenuItem(
            title: store.floatingWidgetVisible ? "隐藏桌面挂件" : "显示桌面挂件",
            action: #selector(toggleFloatingWidget),
            keyEquivalent: ""
        )
        toggleWidget.target = self
        menu.addItem(toggleWidget)

        menu.addItem(.separator())

        let quit = NSMenuItem(title: "退出 TimeDetect", action: #selector(quit), keyEquivalent: "q")
        quit.target = self
        menu.addItem(quit)

        // 在按钮下方弹出，不长期占用 statusItem.menu，左键行为才不会被抢走
        menu.popUp(positioning: nil,
                   at: NSPoint(x: 0, y: sender.bounds.minY - 6),
                   in: sender)
    }

    @objc private func toggleFloatingWidget() {
        store.floatingWidgetVisible.toggle()
    }

    @objc private func quit() {
        NSApp.terminate(nil)
    }

    // MARK: - 状态联动

    private func bindStore() {
        // 峰谷切换瞬间给菜单栏图标一次轻量提示。
        store.$snapshot
            .receive(on: RunLoop.main)
            .sink { [weak self] snapshot in
                guard let self else { return }
                if snapshot.phase != self.lastPhase {
                    self.lastPhase = snapshot.phase
                    self.announce()
                }
            }
            .store(in: &cancellables)

        store.$floatingWidgetVisible
            .receive(on: RunLoop.main)
            .sink { [weak self] _ in self?.syncFloatingWindow() }
            .store(in: &cancellables)
    }

    private func syncFloatingWindow(bringForward: Bool = false) {
        if store.floatingWidgetVisible {
            if floatingWindow == nil {
                floatingWindow = FloatingWidgetWindow(store: store)
            }
            if bringForward {
                floatingWindow?.orderFrontRegardless()
            } else {
                floatingWindow?.orderFront(nil)
            }
        } else {
            floatingWindow?.orderOut(nil)
        }
    }

    /// 切换瞬间让菜单栏图标闪一下。比系统通知更轻，也不需要通知权限。
    private func announce() {
        guard let button = statusItem?.button else { return }
        button.wantsLayer = true
        let flash = CABasicAnimation(keyPath: "opacity")
        flash.fromValue = 1.0
        flash.toValue = 0.35
        flash.duration = 0.28
        flash.autoreverses = true
        flash.repeatCount = 2
        button.layer?.add(flash, forKey: "phaseFlash")
    }
}

