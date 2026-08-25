import Foundation
import Combine

@MainActor
final class DeepSeekStatusManager: ObservableObject {
    @Published private(set) var snapshot: ServiceStatusSnapshot?
    @Published private(set) var isRefreshing = false
    @Published private(set) var isTemporarilyUnavailable = false
    @Published private(set) var lastUpdated: Date?

    static let officialPageURL = URL(string: "https://status.deepseek.com")!

    private let client: HTTPClient
    private var monitorTask: Task<Void, Never>?
    private var requestTask: Task<Void, Never>?

    init(client: HTTPClient = URLSessionHTTPClient()) {
        self.client = client
    }

    func start() {
        guard monitorTask == nil else { return }
        refresh()
        monitorTask = Task { [weak self] in
            while !Task.isCancelled {
                do { try await Task.sleep(nanoseconds: 60 * 1_000_000_000) }
                catch { return }
                guard let self else { return }
                self.refresh()
            }
        }
    }

    func refresh() {
        guard !isRefreshing else { return }
        isRefreshing = true
        let client = client
        requestTask = Task { [weak self] in
            defer {
                self?.isRefreshing = false
                self?.requestTask = nil
            }
            do {
                let value = try await Self.fetch(using: client)
                guard !Task.isCancelled else { return }
                self?.snapshot = value
                self?.lastUpdated = Date()
                self?.isTemporarilyUnavailable = false
            } catch is CancellationError {
                return
            } catch {
                // 网络、HTTP 或解码失败都不代表 DeepSeek 宕机；保留最后成功数据。
                self?.isTemporarilyUnavailable = true
            }
        }
    }

    func stop() {
        monitorTask?.cancel()
        requestTask?.cancel()
        monitorTask = nil
        requestTask = nil
        isRefreshing = false
    }

    func isStale(at date: Date = Date()) -> Bool {
        guard let lastUpdated else { return false }
        return date.timeIntervalSince(lastUpdated) > 5 * 60
    }

    static func fetch(using client: HTTPClient) async throws -> ServiceStatusSnapshot {
        do {
            // DeepSeek 已将状态页迁移到 Flashcat。使用供应商公开 slug 可绕过
            // status.deepseek.com 当前会重置 TLS 连接的问题。
            var request = URLRequest(url: URL(string: "https://statuspage.flashcat.cloud/deepseek")!)
            request.httpMethod = "GET"
            request.setValue("text/html", forHTTPHeaderField: "Accept")
            let response = try await client.send(request)
            guard (200..<300).contains(response.statusCode) else {
                throw HTTPClientError.httpStatus(response.statusCode)
            }
            return try StatuspageParser.flashcatPage(from: response.data)
        } catch is CancellationError {
            throw CancellationError()
        } catch {
            // 保留迁移前 Atlassian Statuspage JSON 的兼容路径，便于官方切回
            // 或部分网络仍命中旧节点时继续工作。
            let response = try await get("summary.json", using: client)
            return try StatuspageParser.summary(from: response.data)
        }
    }

    private static func get(_ path: String, using client: HTTPClient) async throws -> HTTPResponse {
        let url = URL(string: "https://status.deepseek.com/api/v2/\(path)")!
        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        let response = try await client.send(request)
        guard (200..<300).contains(response.statusCode) else {
            throw HTTPClientError.httpStatus(response.statusCode)
        }
        return response
    }
}