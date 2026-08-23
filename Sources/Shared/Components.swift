import SwiftUI

/// 24 小时峰谷时间轴：细线条 + 当前时刻指针。
/// 纯几何绘制，无动画驱动，重绘成本极低。
struct DayTimelineBar: View {
    let snapshot: PhaseSnapshot
    var height: CGFloat = 6
    var showsHourTicks: Bool = true

    private var isWeekend: Bool {
        !PeakEngine.isWeekday(at: snapshot.date)
    }

    private var timelineWindows: [PhaseWindow] {
        isWeekend
            ? [PhaseWindow(startMinute: 0, endMinute: 1440, phase: .offPeak)]
            : PeakEngine.dayWindows
    }

    var body: some View {
        GeometryReader { geo in
            let width = geo.size.width
            ZStack(alignment: .leading) {
                // 底轨
                Capsule(style: .continuous)
                    .fill(Color.white.opacity(0.08))
                    .frame(height: height)

                // 完整绘制峰谷时段；暖橙代表峰时，青蓝代表谷时。
                ForEach(timelineWindows, id: \.self) { window in
                    let x = width * CGFloat(window.startMinute) / 1440
                    let w = width * CGFloat(window.lengthInMinutes) / 1440
                    Capsule(style: .continuous)
                        .fill(isWeekend ? PhaseTheme.weekendBarGradient : window.phase.theme.barGradient)
                        .frame(width: max(w, 2), height: height)
                        .offset(x: x)
                        .opacity(isWeekend || snapshot.phase == window.phase ? 0.96 : 0.58)
                }

                // 整点刻度（06/12/18）
                if showsHourTicks {
                    ForEach([6, 12, 18], id: \.self) { hour in
                        Rectangle()
                            .fill(Color.white.opacity(0.18))
                            .frame(width: 1, height: height + 4)
                            .offset(x: width * CGFloat(hour) / 24)
                    }
                }

                // 当前时刻指针
                Capsule(style: .continuous)
                    .fill(Color.white)
                    .frame(width: 2.5, height: height + 8)
                    .shadow(color: snapshot.phase.theme.glow.opacity(0.9), radius: 4)
                    .offset(x: max(0, min(width - 2.5, width * CGFloat(snapshot.dayProgress))))
            }
            .frame(height: height + 8)
            .frame(maxHeight: .infinity, alignment: .center)
        }
        .frame(height: height + 8)
    }
}

/// 呼吸状态徽章：● 谷时 · 5 折
struct PhaseBadge: View {
    let phase: PricePhase
    var compact: Bool = false

    var body: some View {
        HStack(spacing: compact ? 4 : 5) {
            Circle()
                .fill(phase.theme.gradient)
                .frame(width: compact ? 5 : 6, height: compact ? 5 : 6)
                .shadow(color: phase.theme.glow.opacity(0.8), radius: 3)

            Text("\(phase.shortLabel) · \(compact ? phase.multiplierLabel : phase.priceLabel)")
                .font(.system(size: compact ? 10 : 11, weight: .semibold, design: .rounded))
                .foregroundStyle(Color.white.opacity(0.92))
                .lineLimit(1)
        }
        .padding(.horizontal, compact ? 7 : 9)
        .padding(.vertical, compact ? 3 : 4)
        .background(
            Capsule(style: .continuous)
                .fill(phase.theme.glow.opacity(0.16))
                .overlay(
                    Capsule(style: .continuous)
                        .strokeBorder(phase.theme.glow.opacity(0.35), lineWidth: 0.8)
                )
        )
    }
}

/// 大号名字：梁文锋 / 梁文谷。渐变填充 + 微光，作为视觉主角。
struct PersonaName: View {
    let phase: PricePhase
    var size: CGFloat = 30

    var body: some View {
        Text(phase.personaName)
            .font(.system(size: size, weight: .bold, design: .rounded))
            .kerning(1.5)
            .foregroundStyle(phase.theme.gradient)
            .shadow(color: phase.theme.glow.opacity(0.35), radius: 8)
            .lineLimit(1)
            .minimumScaleFactor(0.7)
    }
}
