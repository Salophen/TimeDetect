import SwiftUI

/// 菜单栏点开后的面板：中尺寸卡片 + 时段说明 + 开关。
struct MenuBarPanel: View {
    @EnvironmentObject private var store: PhaseStore

    var body: some View {
        VStack(spacing: 12) {
            MediumPhaseCard(snapshot: store.snapshot)
                .frame(width: 340, height: 158)

            scheduleLegend

            Divider().opacity(0.15)

            controls
        }
        .padding(14)
        .frame(width: 368)
        .background(Color(red: 0.06, green: 0.07, blue: 0.09))
        .animation(.easeInOut(duration: 0.45), value: store.snapshot.phase)
    }

    /// 当日时段一览，让用户一眼看到四个切换点。
    private var scheduleLegend: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text("今日时段（北京时间）")
                    .font(.system(size: 10, weight: .semibold, design: .rounded))
                    .foregroundStyle(Color.white.opacity(0.5))
                Spacer()
            }

            HStack(spacing: 6) {
                ForEach(PeakEngine.dayWindows, id: \.self) { window in
                    let isCurrent = store.snapshot.beijingMinuteOfDay >= window.startMinute
                        && store.snapshot.beijingMinuteOfDay < window.endMinute
                    VStack(spacing: 3) {
                        Text(PeakEngine.clockText(fromBeijingMinute: window.startMinute))
                            .font(.system(size: 9, weight: .medium, design: .rounded))
                            .monospacedDigit()
                            .foregroundStyle(Color.white.opacity(isCurrent ? 0.9 : 0.4))

                        Capsule()
                            .fill(window.phase.theme.barGradient)
                            .frame(height: isCurrent ? 5 : 3)
                            .opacity(isCurrent ? 1 : 0.45)

                        Text(window.phase.shortLabel)
                            .font(.system(size: 8, weight: .semibold, design: .rounded))
                            .foregroundStyle(Color.white.opacity(isCurrent ? 0.75 : 0.3))
                    }
                    .frame(maxWidth: .infinity)
                }
            }
        }
    }

    private var controls: some View {
        VStack(spacing: 8) {
            Toggle("显示桌面悬浮挂件", isOn: $store.floatingWidgetVisible)
        }
        .toggleStyle(.switch)
        .controlSize(.mini)
        .font(.system(size: 11, weight: .medium, design: .rounded))
        .foregroundStyle(Color.white.opacity(0.8))
        .tint(store.snapshot.phase.theme.accentStart)
    }
}
