import Foundation
import LocalAuthentication
import Security

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
            var item = baseQuery
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
    /// 对旧签名或额外访问控制保护的项目，Keychain 会直接返回错误，由 UI 展示可恢复状态。
    private var nonInteractiveQuery: [String: Any] {
        var query = baseQuery
        let context = LAContext()
        context.interactionNotAllowed = true
        query[kSecUseAuthenticationContext as String] = context
        return query
    }
}