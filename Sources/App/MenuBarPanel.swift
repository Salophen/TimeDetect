import SwiftUI

/// 菜单栏点开后的面板：中尺寸卡片 + 时段说明 + 开关。
struct MenuBarPanel: View {
    @EnvironmentObject private var store: PhaseStore
    @EnvironmentObject private var notificationManager: NotificationManager
    @EnvironmentObject private var launchAtLoginManager: LaunchAtLoginManager
    @EnvironmentObject private var statusManager: DeepSeekStatusManager
    @EnvironmentObject private var balanceManager: DeepSeekBalanceManager
    @Environment(\.openURL) private var openURL
    @State private var showsAPIKeyEditor = false
    @State private var apiKeyInput = ""
    @State private var showsDeleteAPIKeyConfirmation = false

    var body: some View {
        ScrollView(.vertical, showsIndicators: false) {
            VStack(spacing: 12) {
                MediumPhaseCard(snapshot: store.snapshot)
                    .frame(width: 340, height: 150)
                scheduleLegend
                Divider().opacity(0.15)
                serviceStatusSection
                Divider().opacity(0.15)
                balanceSection
                Divider().opacity(0.15)
                controls
            }
            .padding(14)
        }
        .frame(width: 368)
        .frame(maxHeight: 560)
        .background(Color(red: 0.06, green: 0.07, blue: 0.09))
        // 面板自身固定为深色，避免系统控件按桌面浅色外观绘制出黑字黑底。
        .environment(\.colorScheme, .dark)
        .animation(.easeInOut(duration: 0.45), value: store.snapshot.phase)
        .onAppear {
            notificationManager.refreshPermissionState()
            launchAtLoginManager.refresh()
            statusManager.refresh()
            balanceManager.refreshIfStale()
        }
    }

    private var serviceStatusSection: some View {
        VStack(alignment: .leading, spacing: 7) {
            sectionHeader("DeepSeek 服务", isRefreshing: statusManager.isRefreshing) { statusManager.refresh() }
            if let snapshot = statusManager.snapshot {
                statusRow("DeepSeek 整体服务", health: snapshot.overall)
                ForEach(snapshot.services) { statusRow($0.displayName, health: $0.health) }
                if let incident = snapshot.incidents.first {
                    Button { openURL(DeepSeekStatusManager.officialPageURL) } label: {
                        VStack(alignment: .leading, spacing: 2) {
                            Text("▲ \(incident.name)").lineLimit(1)
                            Text(incident.statusText).foregroundStyle(Color.white.opacity(0.45))
                        }
                        .font(.system(size: 10, weight: .medium, design: .rounded))
                    }
                    .buttonStyle(.plain)
                }
            } else {
                Text(statusManager.isRefreshing ? "正在查询官方服务状态" : "○ 状态暂不可用")
                    .foregroundStyle(Color.white.opacity(0.55))
            }
            if statusManager.isTemporarilyUnavailable {
                Text(statusManager.snapshot == nil ? "无法连接官方状态接口" : "○ 当前无法更新，保留上次成功数据")
                    .foregroundStyle(Color.gray.opacity(0.8))
            }
            HStack {
                updateText(statusManager.lastUpdated, stale: statusManager.isStale())
                Spacer()
                Button("查看官方状态 ↗") { openURL(DeepSeekStatusManager.officialPageURL) }.buttonStyle(.link)
            }
            .font(.system(size: 9, weight: .medium, design: .rounded))
        }
        .panelSectionStyle()
    }

    private var balanceSection: some View {
        VStack(alignment: .leading, spacing: 7) {
            sectionHeader("API 账户", isRefreshing: balanceManager.isRefreshing) {
                balanceManager.refresh(force: true)
            }
            if let balance = balanceManager.balance {
                ForEach(balance.balances) { info in
                    HStack(alignment: .firstTextBaseline) {
                        if balance.balances.count > 1 {
                            Text(info.currency)
                                .font(.system(size: 10, weight: .semibold, design: .rounded))
                                .foregroundStyle(Color.white.opacity(0.45))
                        }
                        Text(info.totalText)
                            .font(.system(size: balance.balances.count > 1 ? 17 : 23, weight: .semibold, design: .rounded))
                            .monospacedDigit()
                        Spacer()
                    }
                    Text("赠送 \(info.amountText(info.granted)) · 充值 \(info.amountText(info.toppedUp))")
                        .font(.system(size: 9, weight: .medium, design: .rounded))
                        .foregroundStyle(Color.white.opacity(0.45))
                }
            }
            balanceStateRow
            if balanceManager.isConfigured {
                HStack {
                    Text("API Key 已安全保存在钥匙串中 · •••• \(balanceManager.keySuffix ?? "")")
                    Spacer()
                    Button("更换") { apiKeyInput = ""; showsAPIKeyEditor = true }
                        .buttonStyle(.link)
                        .disabled(balanceManager.isDeletingKey)
                    Button("删除") { showsDeleteAPIKeyConfirmation = true }
                        .buttonStyle(.link)
                        .disabled(balanceManager.isDeletingKey)
                }
                .font(.system(size: 9, weight: .medium, design: .rounded))
                .foregroundStyle(Color.white.opacity(0.45))
            } else if balanceManager.isDeletingKey {
                Text("正在从钥匙串中删除 API Key…")
                    .font(.system(size: 9, weight: .medium, design: .rounded))
                    .foregroundStyle(Color.white.opacity(0.45))
            } else if balanceManager.canRetryKeyDeletion {
                HStack {
                    Text("钥匙串删除失败，凭据可能仍保存在本机")
                    Spacer()
                    Button("重试删除") { balanceManager.retryKeyDeletion() }
                        .buttonStyle(.link)
                }
                .font(.system(size: 9, weight: .medium, design: .rounded))
                .foregroundStyle(Color.orange.opacity(0.82))
            } else if !showsAPIKeyEditor {
                Button("配置 API Key") { showsAPIKeyEditor = true }
                    .buttonStyle(.bordered).controlSize(.small)
            }
            if balanceManager.isConfigured && showsDeleteAPIKeyConfirmation {
                deleteAPIKeyConfirmation
            }
            if showsAPIKeyEditor { apiKeyEditor }
            if balanceManager.lastUpdated != nil { updateText(balanceManager.lastUpdated, stale: false) }
        }
        .panelSectionStyle()
    }

    /// 确认操作留在菜单栏面板内部。系统 alert 会创建面板之外的窗口，
    /// 被外部点击监听识别后会关闭 Popover，导致确认框和删除入口同时消失。
    private var deleteAPIKeyConfirmation: some View {
        VStack(alignment: .leading, spacing: 7) {
            Text("确定永久删除 API Key？")
                .font(.system(size: 10, weight: .semibold, design: .rounded))
                .foregroundStyle(Color.orange.opacity(0.9))
            Text("将从本机 macOS 钥匙串及当前运行状态中彻底清除，此操作无法撤销。")
                .font(.system(size: 9, weight: .medium, design: .rounded))
                .foregroundStyle(Color.white.opacity(0.48))
            HStack {
                Button("永久删除", role: .destructive) {
                    showsDeleteAPIKeyConfirmation = false
                    apiKeyInput = ""
                    showsAPIKeyEditor = false
                    balanceManager.deleteKey()
                }
                .buttonStyle(.borderedProminent)
                .tint(.red)
                .controlSize(.small)
                Button("取消") { showsDeleteAPIKeyConfirmation = false }
                    .buttonStyle(.plain)
            }
        }
        .padding(9)
        .background(Color.orange.opacity(0.08), in: RoundedRectangle(cornerRadius: 8))
        .overlay {
            RoundedRectangle(cornerRadius: 8)
                .stroke(Color.orange.opacity(0.25), lineWidth: 1)
        }
    }

    private var balanceStateRow: some View {
        let warning = [.insufficient, .invalidKey, .rateLimited, .serviceError, .malformedResponse, .keychainError]
            .contains(balanceManager.state)
        return HStack(spacing: 5) {
            Text(warning ? "▲" : (balanceManager.state == .available ? "●" : "○"))
            Text(balanceManager.state.message)
        }
        .font(.system(size: 10, weight: .semibold, design: .rounded))
        .foregroundStyle(warning ? Color.orange.opacity(0.82) : healthColor(balanceManager.state == .available ? .operational : .unknown))
    }

    private var apiKeyEditor: some View {
        VStack(alignment: .leading, spacing: 7) {
            SecureField("DeepSeek API Key", text: $apiKeyInput).textFieldStyle(.roundedBorder)
            Text("API Key 仅保存在本机 macOS 钥匙串中，\n仅用于向 DeepSeek 官方 API 查询账户余额。")
                .font(.system(size: 9, weight: .medium, design: .rounded))
                .foregroundStyle(Color.white.opacity(0.42))
            HStack {
                Button("保存并验证") {
                    balanceManager.saveAndValidate(apiKeyInput); apiKeyInput = ""; showsAPIKeyEditor = false
                }.buttonStyle(.borderedProminent).controlSize(.small)
                Button("取消") { apiKeyInput = ""; showsAPIKeyEditor = false }.buttonStyle(.plain)
            }
        }
        .padding(.top, 3)
    }

    private func sectionHeader(_ title: String, isRefreshing: Bool, action: @escaping () -> Void) -> some View {
        HStack {
            Text(title).font(.system(size: 11, weight: .semibold, design: .rounded))
            Spacer()
            if isRefreshing {
                ProgressView().controlSize(.mini).scaleEffect(0.65)
            } else {
                Button(action: action) { Image(systemName: "arrow.clockwise").font(.system(size: 10, weight: .semibold)) }
                    .buttonStyle(.plain).help("立即刷新")
            }
        }
    }

    private func statusRow(_ name: String, health: ServiceHealth) -> some View {
        HStack(spacing: 6) {
            Text(health == .unknown ? "○" : (health == .operational ? "●" : "▲"))
                .foregroundStyle(healthColor(health))
            Text("\(name) · \(health.title)").foregroundStyle(Color.white.opacity(0.78))
        }
        .font(.system(size: 10, weight: .medium, design: .rounded))
    }

    private func healthColor(_ health: ServiceHealth) -> Color {
        switch health {
        case .operational: return Color(red: 0.36, green: 0.82, blue: 0.62)
        case .degraded: return Color(red: 0.88, green: 0.75, blue: 0.35)
        case .partialOutage, .maintenance: return Color(red: 0.92, green: 0.58, blue: 0.30)
        case .majorOutage: return Color(red: 0.88, green: 0.38, blue: 0.38)
        case .unknown: return Color.gray.opacity(0.75)
        }
    }

    private func updateText(_ date: Date?, stale: Bool) -> some View {
        Group {
            if let date { Text("更新于 \(Self.updateFormatter.string(from: date))\(stale ? " · 数据可能已过期" : "")") }
            else { Text("尚无成功更新") }
        }
        .foregroundStyle(Color.white.opacity(0.35))
    }

    private static let updateFormatter: DateFormatter = {
        let formatter = DateFormatter(); formatter.dateFormat = "HH:mm"; return formatter
    }()

    /// 当日时段一览，让用户一眼看到四个切换点。
    private var scheduleLegend: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text("今日时段（北京时间）")
                    .font(.system(size: 10, weight: .semibold, design: .rounded))
                    .foregroundStyle(Color.white.opacity(0.5))
                Spacer()
            }

            GeometryReader { geometry in
                let windows = scheduleWindows
                let spacing: CGFloat = windows.count > 1 ? 4 : 0
                let availableWidth = max(0, geometry.size.width - spacing * CGFloat(windows.count - 1))

                HStack(spacing: spacing) {
                    ForEach(windows, id: \.self) { window in
                        let isCurrent = store.snapshot.beijingMinuteOfDay >= window.startMinute
                            && store.snapshot.beijingMinuteOfDay < window.endMinute
                        let width = availableWidth * CGFloat(window.lengthInMinutes) / 1440
                        VStack(spacing: 3) {
                            Text(PeakEngine.clockText(fromBeijingMinute: window.startMinute))
                                .font(.system(size: 9, weight: .medium, design: .rounded))
                                .monospacedDigit()
                                .foregroundStyle(isCurrent
                                    ? Color.white.opacity(0.95)
                                    : scheduleColor(for: window).opacity(0.72))

                            Capsule()
                                .fill(scheduleGradient(for: window))
                                .frame(height: isCurrent ? 5 : 3)
                                .opacity(isCurrent ? 1 : 0.72)

                            Text(window.phase.shortLabel)
                                .font(.system(size: 8, weight: .semibold, design: .rounded))
                                .foregroundStyle(isCurrent
                                    ? Color.white.opacity(0.88)
                                    : scheduleColor(for: window).opacity(0.62))
                        }
                        .frame(width: max(width, 2))
                    }
                }
            }
            .frame(height: 43)
        }
    }

    private var scheduleWindows: [PhaseWindow] {
        PeakEngine.isWeekday(at: store.snapshot.date)
            ? PeakEngine.dayWindows
            : [PhaseWindow(startMinute: 0, endMinute: 1440, phase: .offPeak)]
    }

    private func scheduleGradient(for window: PhaseWindow) -> LinearGradient {
        PeakEngine.isWeekday(at: store.snapshot.date)
            ? window.phase.theme.barGradient
            : PhaseTheme.weekendBarGradient
    }

    private func scheduleColor(for window: PhaseWindow) -> Color {
        PeakEngine.isWeekday(at: store.snapshot.date)
            ? window.phase.theme.accentStart
            : Color(red: 0.28, green: 0.58, blue: 1.00)
    }

    private var controls: some View {
        VStack(alignment: .leading, spacing: 10) {
            Toggle("显示桌面悬浮挂件", isOn: $store.floatingWidgetVisible)
                .tint(Color(red: 0.38, green: 0.64, blue: 1.00))

            VStack(alignment: .leading, spacing: 5) {
                Text("显示层级")
                    .font(.system(size: 10, weight: .semibold, design: .rounded))
                    .foregroundStyle(Color.white.opacity(store.floatingWidgetVisible ? 0.5 : 0.25))

                HStack(spacing: 3) {
                    ForEach(FloatingWindowMode.allCases, id: \.self) { mode in
                        modeButton(mode)
                    }
                }
                .padding(3)
                .background(
                    RoundedRectangle(cornerRadius: 7, style: .continuous)
                        .fill(Color.black.opacity(0.32))
                        .overlay(
                            RoundedRectangle(cornerRadius: 7, style: .continuous)
                                .strokeBorder(Color.white.opacity(0.08), lineWidth: 1)
                        )
                )
                .disabled(!store.floatingWidgetVisible)
                .opacity(store.floatingWidgetVisible ? 1 : 0.55)
            }

            Divider().opacity(0.15)

            Toggle("谷时开始提醒", isOn: $store.offPeakNotificationEnabled)
                .tint(PhaseTheme.offPeak.accentStart)

            HStack {
                Toggle("提前提醒", isOn: $store.advanceNotificationEnabled)
                    .disabled(!store.offPeakNotificationEnabled)
                    .tint(PhaseTheme.offPeak.accentStart)
                Spacer()
                Picker("提前时间", selection: $store.advanceNotificationMinutes) {
                    ForEach([5, 10, 15, 30], id: \.self) { minutes in
                        Text("\(minutes) 分钟").tag(minutes)
                    }
                }
                .labelsHidden()
                .pickerStyle(.menu)
                .frame(width: 92)
                .disabled(!store.advanceNotificationEnabled || !store.offPeakNotificationEnabled)
            }

            if notificationManager.permissionState == .denied {
                Text("通知权限未开启")
                    .font(.system(size: 9, weight: .medium, design: .rounded))
                    .foregroundStyle(Color.orange.opacity(0.8))
            }

            Divider().opacity(0.15)

            Toggle("登录 Mac 时自动启动", isOn: Binding(
                get: { launchAtLoginManager.state.isEnabled },
                set: { launchAtLoginManager.setEnabled($0) }
            ))
            .disabled(launchAtLoginManager.state == .unavailable)
            .tint(Color(red: 0.38, green: 0.64, blue: 1.00))

            if launchAtLoginManager.state == .requiresApproval {
                HStack(spacing: 6) {
                    Text("需要在系统设置中批准")
                    Button("打开登录项设置") {
                        launchAtLoginManager.openSettings()
                    }
                    .buttonStyle(.link)
                }
                .font(.system(size: 9, weight: .medium, design: .rounded))
                .foregroundStyle(Color.orange.opacity(0.8))
            } else if launchAtLoginManager.state == .unavailable {
                Text("当前系统不可用（需要 macOS 13 或更高版本）")
                    .font(.system(size: 9, weight: .medium, design: .rounded))
                    .foregroundStyle(Color.white.opacity(0.35))
            } else if launchAtLoginManager.state == .notFound {
                Text("系统未找到当前 App 的登录项服务")
                    .font(.system(size: 9, weight: .medium, design: .rounded))
                    .foregroundStyle(Color.orange.opacity(0.8))
            } else if case .error(let message) = launchAtLoginManager.state {
                Text(message)
                    .font(.system(size: 9, weight: .medium, design: .rounded))
                    .foregroundStyle(Color.orange.opacity(0.8))
                    .lineLimit(2)
            }
        }
        .toggleStyle(.switch)
        .controlSize(.mini)
        .font(.system(size: 11, weight: .medium, design: .rounded))
        .foregroundStyle(Color.white.opacity(0.8))
    }

    private func modeButton(_ mode: FloatingWindowMode) -> some View {
        let isSelected = store.floatingWindowMode == mode
        return Button {
            store.floatingWindowMode = mode
        } label: {
            Text(mode.title)
                .font(.system(size: 10, weight: isSelected ? .semibold : .medium, design: .rounded))
                .foregroundStyle(isSelected ? Color.white : Color.white.opacity(0.58))
                .frame(maxWidth: .infinity)
                .padding(.vertical, 5)
                .background(
                    RoundedRectangle(cornerRadius: 5, style: .continuous)
                        .fill(isSelected
                            ? Color(red: 0.25, green: 0.51, blue: 0.92)
                            : Color.clear)
                )
        }
        .buttonStyle(.plain)
        .accessibilityAddTraits(isSelected ? .isSelected : [])
    }
}

private extension View {
    func panelSectionStyle() -> some View {
        padding(10)
            .background(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .fill(Color.white.opacity(0.035))
                    .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous)
                        .strokeBorder(Color.white.opacity(0.07), lineWidth: 1))
            )
    }
}
