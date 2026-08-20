import SwiftUI

/// 视觉主题：峰时暖橙（锋芒），谷时青绿（省流）。
/// 全部使用固定 sRGB 值，保证在深浅背景与不同显示器下观感一致。
struct PhaseTheme {
    let accentStart: Color
    let accentEnd: Color
    let glow: Color
    let trackDim: Color

    static let peak = PhaseTheme(
        accentStart: Color(red: 1.00, green: 0.75, blue: 0.35),
        accentEnd: Color(red: 1.00, green: 0.39, blue: 0.36),
        glow: Color(red: 1.00, green: 0.51, blue: 0.33),
        trackDim: Color(red: 1.00, green: 0.51, blue: 0.33).opacity(0.16)
    )

    static let offPeak = PhaseTheme(
        accentStart: Color(red: 0.36, green: 0.92, blue: 0.71),
        accentEnd: Color(red: 0.20, green: 0.66, blue: 1.00),
        glow: Color(red: 0.25, green: 0.83, blue: 0.85),
        trackDim: Color(red: 0.25, green: 0.83, blue: 0.85).opacity(0.16)
    )

    static func theme(for phase: PricePhase) -> PhaseTheme {
        phase == .peak ? .peak : .offPeak
    }

    /// 主渐变，用于名字与徽章。
    var gradient: LinearGradient {
        LinearGradient(
            colors: [accentStart, accentEnd],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )
    }

    /// 时间轴用的横向渐变。
    var barGradient: LinearGradient {
        LinearGradient(
            colors: [accentStart, accentEnd],
            startPoint: .leading,
            endPoint: .trailing
        )
    }
}

extension PricePhase {
    var theme: PhaseTheme { PhaseTheme.theme(for: self) }
}

/// 深色磨砂底：与 macOS 桌面/浅色壁纸都能拉开对比，同时保持通透。
struct WidgetBackdrop: View {
    var cornerRadius: CGFloat = 22

    var body: some View {
        ZStack {
            LinearGradient(
                colors: [
                    Color(red: 0.07, green: 0.08, blue: 0.11).opacity(0.94),
                    Color(red: 0.04, green: 0.04, blue: 0.06).opacity(0.97)
                ],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )
            RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                .strokeBorder(Color.white.opacity(0.10), lineWidth: 1)
        }
        .clipShape(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
    }
}
