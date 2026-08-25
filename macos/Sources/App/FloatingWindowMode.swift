import Foundation

enum FloatingWindowMode: String, CaseIterable {
    case desktop
    case alwaysOnTop

    static let defaultMode: FloatingWindowMode = .desktop

    init(storedRawValue: String?) {
        self = storedRawValue.flatMap(Self.init(rawValue:)) ?? Self.defaultMode
    }

    var title: String {
        switch self {
        case .desktop: return "桌面模式"
        case .alwaysOnTop: return "始终置顶"
        }
    }
}