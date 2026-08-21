import Foundation
import Combine

struct CachedBalance: Codable, Equatable, Sendable {
    let snapshot: BalanceSnapshot
    let lastUpdated: Date
    let keySuffix: String
}

protocol BalanceCaching: Sendable {
    func load() async -> CachedBalance?
    func save(_ value: CachedBalance) async
    func clear() async
}

actor UserDefaultsBalanceCache: BalanceCaching {
    private let defaults: UserDefaults
    private let storageKey: String

    init(defaults: UserDefaults = .standard, storageKey: String = "deepSeekBalanceCache") {
        self.defaults = defaults
        self.storageKey = storageKey
    }

    func load() -> CachedBalance? {
        guard let data = defaults.data(forKey: storageKey) else { return nil }
        return try? JSONDecoder().decode(CachedBalance.self, from: data)
    }

    func save(_ value: CachedBalance) {
        guard let data = try? JSONEncoder().encode(value) else { return }
        defaults.set(data, forKey: storageKey)
    }

    func clear() {
        defaults.removeObject(forKey: storageKey)
    }
}

enum BalanceDisplayState: Equatable {
    case unconfigured
    case loading
    case available
    case insufficient
    case invalidKey
    case networkUnavailable
    case rateLimited
    case serviceError
    case malformedResponse
    case keychainError

    var message: String {
        switch self {
        case .unconfigured: return "尚未配置 API Key"
        case .loading: return "正在查询余额"
        case .available: return "可正常调用"
        case .insufficient: return "API 余额不足"
        case .invalidKey: return "API Key 无效"
        case .networkUnavailable: return "当前网络不可用"
        case .rateLimited: return "请求过于频繁，请稍后重试"
        case .serviceError: return "DeepSeek 服务暂时不可用"
        case .malformedResponse: return "余额数据格式异常"
        case .keychainError: return "无法访问 macOS 钥匙串"
        }
    }
}

@MainActor
final class DeepSeekBalanceManager: ObservableObject {
    @Published private(set) var balance: BalanceSnapshot?
    @Published private(set) var state: BalanceDisplayState = .unconfigured
    @Published private(set) var isRefreshing = false
    @Published private(set) var lastUpdated: Date?
    @Published private(set) var keySuffix: String?
    @Published private(set) var isDeletingKey = false
    @Published private(set) var canRetryKeyDeletion = false

    var isConfigured: Bool { apiKey != nil }

    private let client: HTTPClient
    private let keyStore: APIKeyStoring
    private let balanceCache: BalanceCaching
    private var apiKey: String?
    private var monitorTask: Task<Void, Never>?
    private var requestTask: Task<Void, Never>?
    private var startupTask: Task<Void, Never>?
    private var deletionTask: Task<Void, Never>?

    init(
        client: HTTPClient = URLSessionHTTPClient(),
        keyStore: APIKeyStoring = KeychainManager(),
        balanceCache: BalanceCaching = UserDefaultsBalanceCache()
    ) {
        self.client = client
        self.keyStore = keyStore
        self.balanceCache = balanceCache
    }

    /// 恢复钥匙串中的 API Key 和最近一次成功余额，并在后台查询最新余额。
    /// Keychain 查询被配置为禁止交互，因此启动时不会弹出系统认证窗口。
    func start() {
        guard startupTask == nil, apiKey == nil else { return }
        let keyStore = keyStore
        let balanceCache = balanceCache
        startupTask = Task { [weak self] in
            defer { self?.startupTask = nil }
            do {
                guard let storedKey = try await keyStore.read(), !Task.isCancelled else { return }
                guard let self else { return }
                self.setConfiguredKey(storedKey)
                if let cached = await balanceCache.load(),
                   cached.keySuffix == self.keySuffix,
                   !Task.isCancelled {
                    self.balance = cached.snapshot
                    self.lastUpdated = cached.lastUpdated
                    self.state = cached.snapshot.isAvailable ? .available : .insufficient
                } else {
                    self.state = .loading
                }
                self.startMonitoringIfNeeded()
                self.refresh(force: true)
            } catch is CancellationError {
                return
            } catch {
                self?.state = .keychainError
            }
        }
    }

    func refreshIfStale(maxAge: TimeInterval = 60) {
        guard isConfigured else { return }
        if let lastUpdated, Date().timeIntervalSince(lastUpdated) < maxAge { return }
        refresh(force: true)
    }

    func refresh(force: Bool = false) {
        guard let apiKey, !isRefreshing else { return }
        if !force, let lastUpdated, Date().timeIntervalSince(lastUpdated) < 60 { return }
        isRefreshing = true
        if balance == nil { state = .loading }
        let client = client
        requestTask = Task { [weak self] in
            defer {
                self?.isRefreshing = false
                self?.requestTask = nil
            }
            do {
                let value = try await DeepSeekBalanceAPI.fetch(using: client, apiKey: apiKey)
                guard !Task.isCancelled else { return }
                self?.balance = value
                let updatedAt = Date()
                self?.lastUpdated = updatedAt
                self?.state = value.isAvailable ? .available : .insufficient
                if let suffix = self?.keySuffix {
                    await self?.balanceCache.save(CachedBalance(
                        snapshot: value,
                        lastUpdated: updatedAt,
                        keySuffix: suffix
                    ))
                }
            } catch is CancellationError {
                return
            } catch let error as BalanceAPIError {
                self?.apply(error)
            } catch {
                // URLSession 传输错误不代表余额不足或 DeepSeek 官方服务异常。
                self?.state = .networkUnavailable
            }
        }
    }

    func saveAndValidate(_ input: String) {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            state = .invalidKey
            return
        }
        guard !isRefreshing, !isDeletingKey else { return }
        let keyStore = keyStore
        startupTask?.cancel()
        startupTask = Task { [weak self] in
            defer { self?.startupTask = nil }
            do {
                try await keyStore.save(trimmed)
                guard !Task.isCancelled, let self else { return }
                self.setConfiguredKey(trimmed)
                self.canRetryKeyDeletion = false
                self.balance = nil
                self.lastUpdated = nil
                self.state = .loading
                await self.balanceCache.clear()
                self.startMonitoringIfNeeded()
                self.refresh(force: true)
            } catch is CancellationError {
                return
            } catch {
                self?.state = .keychainError
            }
        }
    }

    func deleteKey() {
        guard !isDeletingKey else { return }
        let keyStore = keyStore
        startupTask?.cancel()
        requestTask?.cancel()
        monitorTask?.cancel()
        // 立即清除内存与展示缓存，确保删除期间旧 Key 也不会再用于请求。
        clearConfiguration()
        isDeletingKey = true
        canRetryKeyDeletion = false
        deletionTask = Task { [weak self] in
            defer {
                self?.isDeletingKey = false
                self?.deletionTask = nil
            }
            do {
                await self?.balanceCache.clear()
                try await keyStore.delete()
                // 删除后回读验证，避免仅凭删除调用未报错就向用户宣告成功。
                guard try await keyStore.read() == nil else {
                    throw KeychainError.deletionVerificationFailed
                }
            } catch is CancellationError {
                return
            } catch {
                self?.state = .keychainError
                self?.canRetryKeyDeletion = true
            }
        }
    }

    func retryKeyDeletion() {
        guard canRetryKeyDeletion, !isDeletingKey else { return }
        deleteKey()
    }

    func stop() {
        startupTask?.cancel()
        deletionTask?.cancel()
        monitorTask?.cancel()
        requestTask?.cancel()
        startupTask = nil
        monitorTask = nil
        requestTask = nil
        deletionTask = nil
        isRefreshing = false
        isDeletingKey = false
    }

    private func setConfiguredKey(_ value: String) {
        apiKey = value
        keySuffix = String(value.suffix(4))
    }

    private func startMonitoringIfNeeded() {
        guard monitorTask == nil else { return }
        monitorTask = Task { [weak self] in
            while !Task.isCancelled {
                do { try await Task.sleep(nanoseconds: 5 * 60 * 1_000_000_000) }
                catch { return }
                guard let self else { return }
                self.refresh(force: true)
            }
        }
    }

    private func clearConfiguration() {
        apiKey = nil
        keySuffix = nil
        balance = nil
        lastUpdated = nil
        isRefreshing = false
        state = .unconfigured
        monitorTask = nil
        requestTask = nil
    }

    private func apply(_ error: BalanceAPIError) {
        switch error {
        case .invalidKey: state = .invalidKey
        case .insufficientBalance: state = .insufficient
        case .rateLimited: state = .rateLimited
        case .serviceUnavailable, .unexpectedHTTP: state = .serviceError
        case .malformedResponse: state = .malformedResponse
        }
    }
}