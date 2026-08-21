import SwiftUI

/// 小尺寸方形卡片（对应 WidgetKit systemSmall，也用于桌面悬浮挂件）。
///
/// 排版骨架（自上而下四层，间距按 8pt 栅格）：
///   1. 顶栏：DEEPSEEK 字标 + 时段徽章
///   2. 主角：梁文锋 / 梁文谷（渐变大字）+ 英文副标
///   3. 时钟：实时 HH:mm:ss（等宽数字，不会左右跳动）
///   4. 底部：24h 时间轴 + 距切换倒计时
struct SmallPhaseCard: View {
    let snapshot: PhaseSnapshot
    /// true 时时钟由系统 Text(style:) 驱动（Widget 省电模式）。
    var usesSystemTimer: Bool = false
    /// 仅桌面悬浮窗使用；Widget 默认不显示，保持原有紧凑布局。
    var accessory: AnyView? = nil

    private var phase: PricePhase { snapshot.phase }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header

            Spacer(minLength: 6)

            PersonaName(phase: phase, size: 32)

            Text(phase.latinLabel)
                .font(.system(size: 9, weight: .heavy, design: .rounded))
                .kerning(2.2)
                .foregroundStyle(Color.white.opacity(0.32))
                .padding(.top, 3)

            Spacer(minLength: 8)

            clock

            if let accessory {
                accessory
                    .padding(.top, 8)
            }

            Spacer(minLength: 8)

            footer
        }
        .padding(14)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .background(WidgetBackdrop(cornerRadius: 22))
        // 顶部一层与时段同色的柔光，切换时整卡氛围随之变化
        .overlay(alignment: .topTrailing) {
            RadialGradient(
                colors: [phase.theme.glow.opacity(0.28), .clear],
                center: .topTrailing,
                startRadius: 0,
                endRadius: 130
            )
            .allowsHitTesting(false)
            .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
        }
    }

    private var header: some View {
        HStack(spacing: 6) {
            Text("DEEPSEEK")
                .font(.system(size: 9, weight: .black, design: .rounded))
                .kerning(1.6)
                .foregroundStyle(Color.white.opacity(0.45))

            Spacer(minLength: 4)

            PhaseBadge(phase: phase, compact: true)
        }
    }

    private var clock: some View {
        HStack(alignment: .firstTextBaseline, spacing: 5) {
            if usesSystemTimer {
                // Widget 内由系统渲染，不唤醒进程
                Text(snapshot.date, style: .time)
                    .font(.system(size: 22, weight: .medium, design: .rounded))
                    .monospacedDigit()
                    .foregroundStyle(Color.white.opacity(0.95))
            } else {
                Text(Self.clockFormatter.string(from: snapshot.date))
                    .font(.system(size: 22, weight: .medium, design: .rounded))
                    .monospacedDigit()
                    .foregroundStyle(Color.white.opacity(0.95))
            }

            if !snapshot.isLocalBeijing {
                Text("北京")
                    .font(.system(size: 8, weight: .semibold, design: .rounded))
                    .foregroundStyle(Color.white.opacity(0.35))
            }
        }
    }

    private var footer: some View {
        VStack(alignment: .leading, spacing: 6) {
            DayTimelineBar(snapshot: snapshot, height: 5)

            HStack(spacing: 4) {
                Text("转\(phase.opposite.shortLabel)")
                    .font(.system(size: 9, weight: .medium, design: .rounded))
                    .foregroundStyle(Color.white.opacity(0.38))

                if usesSystemTimer {
                    Text(snapshot.nextBoundary, style: .timer)
                        .font(.system(size: 10, weight: .semibold, design: .rounded))
                        .monospacedDigit()
                        .foregroundStyle(Color.white.opacity(0.72))
                } else {
                    Text(PeakEngine.countdownText(snapshot.secondsToNextBoundary))
                        .font(.system(size: 10, weight: .semibold, design: .rounded))
                        .monospacedDigit()
                        .foregroundStyle(Color.white.opacity(0.72))
                }

                Spacer(minLength: 0)

                Text(PeakEngine.clockText(fromBeijingMinute: nextBoundaryMinute))
                    .font(.system(size: 9, weight: .medium, design: .rounded))
                    .monospacedDigit()
                    .foregroundStyle(Color.white.opacity(0.38))
            }
        }
    }

    private var nextBoundaryMinute: Int {
        Int(PeakEngine.beijingMinutes(for: snapshot.nextBoundary).rounded())
    }

    static let clockFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm:ss"
        formatter.timeZone = PeakEngine.beijingTimeZone
        return formatter
    }()
}

/// 中尺寸宽条卡片（对应 WidgetKit systemMedium）：左侧主角，右侧时钟 / 价目 / 时间轴。
struct MediumPhaseCard: View {
    let snapshot: PhaseSnapshot
    var usesSystemTimer: Bool = false

    private var phase: PricePhase { snapshot.phase }

    var body: some View {
        HStack(spacing: 16) {
            leftColumn

            // 竖向细线，强化左右分区
            Rectangle()
                .fill(Color.white.opacity(0.08))
                .frame(width: 1)

            rightColumn
        }
        .padding(16)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .background(WidgetBackdrop(cornerRadius: 22))
        .overlay(alignment: .topTrailing) {
            RadialGradient(
                colors: [phase.theme.glow.opacity(0.22), .clear],
                center: .topTrailing,
                startRadius: 0,
                endRadius: 200
            )
            .allowsHitTesting(false)
            .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
        }
    }

    private var leftColumn: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text("DEEPSEEK")
                .font(.system(size: 9, weight: .black, design: .rounded))
                .kerning(1.6)
                .foregroundStyle(Color.white.opacity(0.45))

            Spacer(minLength: 8)

            PersonaName(phase: phase, size: 34)

            Text(phase.latinLabel)
                .font(.system(size: 9, weight: .heavy, design: .rounded))
                .kerning(2.2)
                .foregroundStyle(Color.white.opacity(0.32))
                .padding(.top, 3)

            Spacer(minLength: 8)

            PhaseBadge(phase: phase)
        }
    }


    private var rightColumn: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                if usesSystemTimer {
                    Text(snapshot.date, style: .time)
                        .font(.system(size: 30, weight: .medium, design: .rounded))
                        .monospacedDigit()
                        .foregroundStyle(Color.white.opacity(0.96))
                } else {
                    Text(SmallPhaseCard.clockFormatter.string(from: snapshot.date))
                        .font(.system(size: 30, weight: .medium, design: .rounded))
                        .monospacedDigit()
                        .foregroundStyle(Color.white.opacity(0.96))
                }

                Text(snapshot.isLocalBeijing ? "CST" : "北京")
                    .font(.system(size: 9, weight: .semibold, design: .rounded))
                    .foregroundStyle(Color.white.opacity(0.35))
            }

            Spacer(minLength: 10)

            priceRow

            Spacer(minLength: 12)

            DayTimelineBar(snapshot: snapshot, height: 6)

            HStack(spacing: 5) {
                Text("距\(phase.opposite.shortLabel)")
                    .font(.system(size: 10, weight: .medium, design: .rounded))
                    .foregroundStyle(Color.white.opacity(0.38))

                if usesSystemTimer {
                    Text(snapshot.nextBoundary, style: .timer)
                        .font(.system(size: 11, weight: .semibold, design: .rounded))
                        .monospacedDigit()
                        .foregroundStyle(Color.white.opacity(0.75))
                } else {
                    Text(PeakEngine.countdownText(snapshot.secondsToNextBoundary))
                        .font(.system(size: 11, weight: .semibold, design: .rounded))
                        .monospacedDigit()
                        .foregroundStyle(Color.white.opacity(0.75))
                }

                Spacer(minLength: 0)

                Text("09-12 / 14-18 峰")
                    .font(.system(size: 9, weight: .medium, design: .rounded))
                    .foregroundStyle(Color.white.opacity(0.30))
            }
            .padding(.top, 6)
        }
    }

    /// 只展示稳定的倍率规则，不硬编码可能随模型与官方定价调整的绝对单价。
    private var priceRow: some View {
        HStack(spacing: 14) {
            priceItem(title: "倍率", value: phase.multiplierLabel)
            priceItem(title: "计费", value: phase.priceKindLabel)
        }
    }

    private func priceItem(title: String, value: String) -> some View {
        VStack(alignment: .leading, spacing: 1) {
            Text(title)
                .font(.system(size: 9, weight: .medium, design: .rounded))
                .foregroundStyle(Color.white.opacity(0.35))
            Text(value)
                .font(.system(size: 13, weight: .semibold, design: .rounded))
                .monospacedDigit()
                .foregroundStyle(phase.theme.gradient)
        }
    }
}
