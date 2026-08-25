import Foundation

enum ServiceHealth: String, Equatable, Sendable {
    case operational
    case degraded
    case partialOutage
    case majorOutage
    case maintenance
    case unknown

    static func overallIndicator(_ value: String) -> ServiceHealth {
        switch value.lowercased() {
        case "none": return .operational
        case "minor": return .degraded
        case "major": return .partialOutage
        case "critical": return .majorOutage
        default: return .unknown
        }
    }

    static func componentStatus(_ value: String) -> ServiceHealth {
        switch value.lowercased() {
        case "operational": return .operational
        case "degraded_performance": return .degraded
        case "degraded": return .degraded
        case "partial_outage": return .partialOutage
        case "major_outage", "full_outage": return .majorOutage
        case "under_maintenance", "maintenance": return .maintenance
        default: return .unknown
        }
    }

    /// 聚合多个组件时保留最严重的状态；未知状态不能被误报为正常。
    static func worst(_ values: [ServiceHealth]) -> ServiceHealth {
        values.max { severity($0) < severity($1) } ?? .unknown
    }

    private static func severity(_ value: ServiceHealth) -> Int {
        switch value {
        case .operational: return 0
        case .maintenance: return 1
        case .degraded: return 2
        case .partialOutage: return 3
        case .majorOutage: return 4
        case .unknown: return 5
        }
    }

    var title: String {
        switch self {
        case .operational: return "服务正常"
        case .degraded: return "性能下降"
        case .partialOutage: return "部分中断"
        case .majorOutage: return "严重中断"
        case .maintenance: return "维护中"
        case .unknown: return "状态未知"
        }
    }
}

struct MonitoredService: Equatable, Sendable, Identifiable {
    enum Kind: String, Sendable { case api, webChat }
    let kind: Kind
    let officialName: String
    let health: ServiceHealth
    var id: Kind { kind }
    var displayName: String { kind == .api ? "API 服务" : "网页对话服务" }
}

struct ServiceIncident: Equatable, Sendable, Identifiable {
    let id: String
    let name: String
    let status: String

    var statusText: String {
        switch status.lowercased() {
        case "investigating": return "正在调查"
        case "identified": return "问题已确认"
        case "monitoring": return "正在监控"
        case "resolved": return "已解决"
        default: return status
        }
    }
}

struct ServiceStatusSnapshot: Equatable, Sendable {
    let overall: ServiceHealth
    let services: [MonitoredService]
    let incidents: [ServiceIncident]
}

private struct StatuspageStatus: Decodable { let indicator: String }
private struct StatuspageComponent: Decodable { let name: String; let status: String }
private struct StatuspageIncident: Decodable { let id: String; let name: String; let status: String }
private struct StatuspageSummary: Decodable {
    let status: StatuspageStatus
    let components: [StatuspageComponent]
    let incidents: [StatuspageIncident]
    let scheduledMaintenances: [StatuspageIncident]?
    enum CodingKeys: String, CodingKey {
        case status, components, incidents
        case scheduledMaintenances = "scheduled_maintenances"
    }
}
private struct StatuspageStatusEnvelope: Decodable { let status: StatuspageStatus }
private struct StatuspageComponentsEnvelope: Decodable { let components: [StatuspageComponent] }
private struct StatuspageIncidentsEnvelope: Decodable { let incidents: [StatuspageIncident] }

private struct FlashcatPayload: Decodable {
    struct Page: Decodable {
        let pageID: Int64
        let name: String
        let customDomain: String
        let components: [Component]
        let sections: [Section]
        enum CodingKeys: String, CodingKey {
            case pageID = "page_id", name, components, sections
            case customDomain = "custom_domain"
        }
    }
    struct Component: Decodable {
        let componentID: String
        let sectionID: String?
        let name: String
        let status: String?
        enum CodingKeys: String, CodingKey {
            case componentID = "component_id"
            case sectionID = "section_id"
            case name, status
        }
    }
    struct Section: Decodable {
        let sectionID: String
        let name: String
        enum CodingKeys: String, CodingKey { case sectionID = "section_id", name }
    }
    struct Change: Decodable {
        let changeID: Int64
        let title: String
        let status: String
        let type: String
        let affectedComponents: [Component]
        enum CodingKeys: String, CodingKey {
            case changeID = "change_id"
            case title, status, type
            case affectedComponents = "affected_components"
        }
    }
    let page: Page
    let activeChanges: [Change]
    enum CodingKeys: String, CodingKey { case page; case activeChanges = "active_changes" }
}

enum StatuspageParser {
    private static let deepSeekFlashcatPageID: Int64 = 6_410_630_422_455

    static func summary(from data: Data) throws -> ServiceStatusSnapshot {
        let payload = try JSONDecoder().decode(StatuspageSummary.self, from: data)
        let activeMaintenance = (payload.scheduledMaintenances ?? []).filter { $0.status.lowercased() != "completed" }
        return makeSnapshot(
            status: payload.status,
            components: payload.components,
            incidents: payload.incidents + activeMaintenance
        )
    }

    static func combined(statusData: Data, componentsData: Data, incidentsData: Data) throws -> ServiceStatusSnapshot {
        let status = try JSONDecoder().decode(StatuspageStatusEnvelope.self, from: statusData).status
        let components = try JSONDecoder().decode(StatuspageComponentsEnvelope.self, from: componentsData).components
        let incidents = try JSONDecoder().decode(StatuspageIncidentsEnvelope.self, from: incidentsData).incidents
        return makeSnapshot(status: status, components: components, incidents: incidents)
    }

    /// Flashcat 状态页由 Next.js 服务端渲染，公开状态数据以 JSON 形式放在
    /// `self.__next_f.push` 的 `initialData` 中。这里只解析该结构化 JSON，不根据
    /// 页面文案或“可访问”与否猜测服务状态。
    static func flashcatPage(from data: Data) throws -> ServiceStatusSnapshot {
        guard data.count <= 2_000_000,
              let html = String(data: data, encoding: .utf8),
              let payloadData = nextInitialData(in: html) else {
            throw FlashcatParseError.invalidPayload
        }
        let payload = try JSONDecoder().decode(FlashcatPayload.self, from: payloadData)
        guard payload.page.pageID == deepSeekFlashcatPageID,
              payload.page.name.caseInsensitiveCompare("DeepSeek") == .orderedSame,
              payload.page.customDomain.lowercased() == "status.deepseek.com" else {
            throw FlashcatParseError.unexpectedPage
        }

        var currentStatuses: [String: ServiceHealth] = [:]
        for component in payload.page.components {
            // 避免畸形响应中的重复 ID 触发 Dictionary 初始化器的 precondition。
            currentStatuses[component.componentID] = ServiceHealth.componentStatus(component.status ?? "operational")
        }
        for change in payload.activeChanges {
            for component in change.affectedComponents {
                currentStatuses[component.componentID] = ServiceHealth.componentStatus(component.status ?? "unknown")
            }
        }

        let chatSectionIDs = Set(payload.page.sections.filter {
            let name = $0.name.lowercased()
            return name.contains("chat") || name.contains("对话") || name.contains("聊天")
        }.map(\.sectionID))
        let apiComponents = payload.page.components.filter { matches($0.name, kind: .api) }
        let chatComponents = payload.page.components.filter {
            guard let sectionID = $0.sectionID else { return matches($0.name, kind: .webChat) }
            return chatSectionIDs.contains(sectionID) || matches($0.name, kind: .webChat)
        }
        let services = [
            flashcatService(.api, components: apiComponents, statuses: currentStatuses),
            flashcatService(.webChat, components: chatComponents, statuses: currentStatuses)
        ]
        let allHealth = payload.page.components.map { currentStatuses[$0.componentID] ?? .unknown }
        return ServiceStatusSnapshot(
            overall: ServiceHealth.worst(allHealth),
            services: services,
            incidents: payload.activeChanges.map {
                ServiceIncident(id: String($0.changeID), name: $0.title, status: $0.status)
            }
        )
    }

    private static func flashcatService(
        _ kind: MonitoredService.Kind,
        components: [FlashcatPayload.Component],
        statuses: [String: ServiceHealth]
    ) -> MonitoredService {
        MonitoredService(
            kind: kind,
            officialName: components.map(\.name).joined(separator: ", "),
            health: ServiceHealth.worst(components.map { statuses[$0.componentID] ?? .unknown })
        )
    }

    private static func nextInitialData(in html: String) -> Data? {
        let pushMarker = "self.__next_f.push([1,"
        let dataMarker = "\"initialData\":"
        var searchStart = html.startIndex
        while let markerRange = html.range(of: pushMarker, range: searchStart..<html.endIndex) {
            guard let quote = html[markerRange.upperBound...].firstIndex(of: "\"") else { return nil }
            guard let literalEnd = jsonStringEnd(in: html, startingAt: quote) else { return nil }
            let literal = String(html[quote...literalEnd])
            if let wrapperData = "[\(literal)]".data(using: .utf8),
               let text = (try? JSONDecoder().decode([String].self, from: wrapperData))?.first,
               let marker = text.range(of: dataMarker),
               let objectStart = text[marker.upperBound...].firstIndex(of: "{"),
               let objectEnd = jsonObjectEnd(in: text, startingAt: objectStart) {
                return String(text[objectStart...objectEnd]).data(using: .utf8)
            }
            searchStart = html.index(after: literalEnd)
        }
        return nil
    }

    private static func jsonStringEnd(in text: String, startingAt start: String.Index) -> String.Index? {
        var index = text.index(after: start)
        var escaped = false
        while index < text.endIndex {
            let character = text[index]
            if escaped { escaped = false }
            else if character == "\\" { escaped = true }
            else if character == "\"" { return index }
            index = text.index(after: index)
        }
        return nil
    }

    private static func jsonObjectEnd(in text: String, startingAt start: String.Index) -> String.Index? {
        var index = start
        var depth = 0
        var inString = false
        var escaped = false
        while index < text.endIndex {
            let character = text[index]
            if inString {
                if escaped { escaped = false }
                else if character == "\\" { escaped = true }
                else if character == "\"" { inString = false }
            } else if character == "\"" { inString = true }
            else if character == "{" { depth += 1 }
            else if character == "}" {
                depth -= 1
                if depth == 0 { return index }
            }
            index = text.index(after: index)
        }
        return nil
    }

    private static func makeSnapshot(
        status: StatuspageStatus,
        components: [StatuspageComponent],
        incidents: [StatuspageIncident]
    ) -> ServiceStatusSnapshot {
        let services = [MonitoredService.Kind.api, .webChat].map { kind in
            let component = components.first { matches($0.name, kind: kind) }
            return MonitoredService(
                kind: kind,
                officialName: component?.name ?? "",
                health: component.map { ServiceHealth.componentStatus($0.status) } ?? .unknown
            )
        }
        return ServiceStatusSnapshot(
            overall: ServiceHealth.overallIndicator(status.indicator),
            services: services,
            incidents: incidents.map { ServiceIncident(id: $0.id, name: $0.name, status: $0.status) }
        )
    }

    static func matches(_ name: String, kind: MonitoredService.Kind) -> Bool {
        let normalized = name.lowercased()
            .replacingOccurrences(of: "-", with: " ")
            .replacingOccurrences(of: "_", with: " ")
        switch kind {
        case .api:
            return normalized.contains("api") || normalized.contains("接口")
        case .webChat:
            return (normalized.contains("web") && normalized.contains("chat"))
                || normalized.contains("网页对话") || normalized.contains("网页聊天")
        }
    }
}

enum FlashcatParseError: Error, Equatable {
    case invalidPayload
    case unexpectedPage
}

struct BalanceInfo: Codable, Equatable, Sendable, Identifiable {
    let currency: String
    let total: Decimal
    let granted: Decimal
    let toppedUp: Decimal
    var id: String { currency }

    func amountText(_ amount: Decimal) -> String {
        let symbol = currency == "CNY" ? "¥" : (currency == "USD" ? "$" : "\(currency) ")
        return symbol + Self.numberFormatter.string(from: amount as NSDecimalNumber)!
    }

    var totalText: String { amountText(total) }

    private static let numberFormatter: NumberFormatter = {
        let formatter = NumberFormatter()
        formatter.numberStyle = .decimal
        formatter.minimumFractionDigits = 2
        formatter.maximumFractionDigits = 8
        formatter.usesGroupingSeparator = false
        return formatter
    }()
}

struct BalanceSnapshot: Codable, Equatable, Sendable {
    let isAvailable: Bool
    let balances: [BalanceInfo]
}

private struct BalancePayload: Decodable {
    struct Info: Decodable {
        let currency: String
        let totalBalance: String
        let grantedBalance: String
        let toppedUpBalance: String
        enum CodingKeys: String, CodingKey {
            case currency
            case totalBalance = "total_balance"
            case grantedBalance = "granted_balance"
            case toppedUpBalance = "topped_up_balance"
        }
    }
    let isAvailable: Bool
    let balanceInfos: [Info]
    enum CodingKeys: String, CodingKey {
        case isAvailable = "is_available"
        case balanceInfos = "balance_infos"
    }
}

enum BalanceParseError: Error, Equatable { case invalidAmount }

enum BalanceParser {
    static func parse(_ data: Data) throws -> BalanceSnapshot {
        let payload = try JSONDecoder().decode(BalancePayload.self, from: data)
        let infos = try payload.balanceInfos.map { info -> BalanceInfo in
            guard let total = Decimal(string: info.totalBalance, locale: Locale(identifier: "en_US_POSIX")),
                  let granted = Decimal(string: info.grantedBalance, locale: Locale(identifier: "en_US_POSIX")),
                  let toppedUp = Decimal(string: info.toppedUpBalance, locale: Locale(identifier: "en_US_POSIX")) else {
                throw BalanceParseError.invalidAmount
            }
            return BalanceInfo(currency: info.currency.uppercased(), total: total, granted: granted, toppedUp: toppedUp)
        }
        return BalanceSnapshot(isAvailable: payload.isAvailable, balances: infos)
    }
}

enum BalanceAPIError: Error, Equatable {
    case invalidKey
    case insufficientBalance
    case rateLimited
    case serviceUnavailable(Int)
    case malformedResponse
    case unexpectedHTTP(Int)
}

enum DeepSeekBalanceAPI {
    static let endpoint = URL(string: "https://api.deepseek.com/user/balance")!

    static func fetch(using client: HTTPClient, apiKey: String) async throws -> BalanceSnapshot {
        var request = URLRequest(url: endpoint)
        request.httpMethod = "GET"
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("Bearer \(apiKey)", forHTTPHeaderField: "Authorization")
        let response = try await client.send(request)
        switch response.statusCode {
        case 200:
            do { return try BalanceParser.parse(response.data) }
            catch { throw BalanceAPIError.malformedResponse }
        case 401: throw BalanceAPIError.invalidKey
        case 402: throw BalanceAPIError.insufficientBalance
        case 429: throw BalanceAPIError.rateLimited
        case 500, 503: throw BalanceAPIError.serviceUnavailable(response.statusCode)
        default: throw BalanceAPIError.unexpectedHTTP(response.statusCode)
        }
    }
}