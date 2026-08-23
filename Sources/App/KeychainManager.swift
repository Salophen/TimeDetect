import Foundation
import Security
import LocalAuthentication

protocol APIKeyStoring: Sendable {
    func read() async throws -> String?
    func save(_ value: String) async throws
    func delete() async throws
}

enum KeychainError: Error, Equatable {
    case unexpectedStatus(OSStatus)
    case invalidData
    case deletionVerificationFailed
}

actor KeychainManager: APIKeyStoring {
    private let service: String
    private let account: String

    init(service: String = "local.timedetect.app.deepseek", account: String = "api-key") {
        self.service = service
        self.account = account
    }

    func read() throws -> String? {
        var query = nonInteractiveQuery
        query[kSecReturnData as String] = true
        query[kSecMatchLimit as String] = kSecMatchLimitOne
        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        if status == errSecItemNotFound { return nil }
        guard status == errSecSuccess else { throw KeychainError.unexpectedStatus(status) }
        guard let data = result as? Data, let value = String(data: data, encoding: .utf8) else {
            throw KeychainError.invalidData
        }
        return value
    }

    func save(_ value: String) throws {
        let data = Data(value.utf8)
        let status = SecItemUpdate(
            nonInteractiveQuery as CFDictionary,
            [kSecValueData as String: data] as CFDictionary
        )
        if status == errSecItemNotFound {
            var item = nonInteractiveQuery
            item[kSecValueData as String] = data
            item[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
            let addStatus = SecItemAdd(item as CFDictionary, nil)
            guard addStatus == errSecSuccess else { throw KeychainError.unexpectedStatus(addStatus) }
        } else if status != errSecSuccess {
            throw KeychainError.unexpectedStatus(status)
        }
    }

    func delete() throws {
        let status = SecItemDelete(nonInteractiveQuery as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else {
            throw KeychainError.unexpectedStatus(status)
        }
    }

    private var baseQuery: [String: Any] {
        [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]
    }

    /// 后台启动、保存和删除都不得唤起 macOS 密码认证窗口。
    ///
    /// API Key 不需要生物识别或用户在场认证。只使用 Security 提供的
    /// `kSecUseAuthenticationUIFail`，让启动时的读写在任何情况下都不弹出
    /// 密码窗口；如果钥匙串项目确实受额外访问控制保护，则返回错误交给
    /// 上层展示可恢复状态。
    private var nonInteractiveQuery: [String: Any] {
        var query = baseQuery
        let context = LAContext()
        context.interactionNotAllowed = true
        query[kSecUseAuthenticationContext as String] = context
        return query
    }
}

/// API Key 的本地存储。
///
/// API Key 不再放入可能触发系统认证的 Keychain 项目，而是直接保存在本 App
/// 的 UserDefaults 中。这样应用启动时不会访问 Keychain，也不会弹出密码窗口。
/// 旧版本的 Keychain 项目只用于一次性、非交互式迁移和删除。
actor UserDefaultsAPIKeyStore: APIKeyStoring {
    private let defaults: UserDefaults
    private let storageKey: String
    private let legacyKeychain: KeychainManager

    init(
        defaults: UserDefaults = .standard,
        storageKey: String = "deepSeekAPIKey",
        legacyKeychain: KeychainManager = KeychainManager()
    ) {
        self.defaults = defaults
        self.storageKey = storageKey
        self.legacyKeychain = legacyKeychain
    }

    func read() async throws -> String? {
        if let value = defaults.string(forKey: storageKey), !value.isEmpty {
            return value
        }

        // 兼容已经配置过的旧版本；KeychainManager 明确禁止交互认证。
        guard let legacyValue = try? await legacyKeychain.read() else { return nil }
        if !legacyValue.isEmpty {
            defaults.set(legacyValue, forKey: storageKey)
            return legacyValue
        }
        return nil
    }

    func save(_ value: String) async throws {
        defaults.set(value, forKey: storageKey)
        // 新值已由 UserDefaults 保存，尽力删除旧 Keychain 项目；失败不影响使用。
        try? await legacyKeychain.delete()
    }

    func delete() async throws {
        defaults.removeObject(forKey: storageKey)
        try? await legacyKeychain.delete()
    }
}