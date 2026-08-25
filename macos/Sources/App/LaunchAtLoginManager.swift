import Foundation
import ServiceManagement

@MainActor
final class LaunchAtLoginManager: ObservableObject {
    enum State: Equatable {
        case unavailable
        case notRegistered
        case enabled
        case requiresApproval
        case notFound
        case error(String)

        var isEnabled: Bool { self == .enabled }
    }

    @Published private(set) var state: State = .unavailable

    func refresh() {
        guard #available(macOS 13.0, *) else {
            state = .unavailable
            return
        }
        state = map(SMAppService.mainApp.status)
    }

    func setEnabled(_ enabled: Bool) {
        guard #available(macOS 13.0, *) else { return }
        do {
            if enabled {
                try SMAppService.mainApp.register()
            } else {
                try SMAppService.mainApp.unregister()
            }
            refresh()
        } catch {
            state = .error(error.localizedDescription)
        }
    }

    func openSettings() {
        guard #available(macOS 13.0, *) else { return }
        SMAppService.openSystemSettingsLoginItems()
    }

    @available(macOS 13.0, *)
    private func map(_ status: SMAppService.Status) -> State {
        switch status {
        case .notRegistered: return .notRegistered
        case .enabled: return .enabled
        case .requiresApproval: return .requiresApproval
        case .notFound: return .notFound
        @unknown default: return .notFound
        }
    }
}