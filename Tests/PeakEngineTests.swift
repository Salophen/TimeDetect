import Foundation

@inline(__always)
func check(_ condition: @autoclosure () -> Bool, _ message: String) {
    guard condition() else {
        fputs("FAIL: \(message)\n", stderr)
        exit(1)
    }
}

func beijingDate(_ hour: Int, _ minute: Int, _ second: Int = 0, dayOffset: Int = 0) -> Date {
    var calendar = Calendar(identifier: .gregorian)
    calendar.timeZone = PeakEngine.beijingTimeZone
    let day = calendar.date(from: DateComponents(year: 2026, month: 8, day: 20 + dayOffset))!
    return day.addingTimeInterval(Double(hour * 3600 + minute * 60 + second))
}

actor MockHTTPClient: HTTPClient {
    enum Result {
        case response(HTTPResponse)
        case failure(URLError.Code)
    }
    private var results: [Result]
    private(set) var requests: [URLRequest] = []

    init(_ responses: [HTTPResponse]) { results = responses.map(Result.response) }
    init(results: [Result]) { self.results = results }

    func send(_ request: URLRequest) async throws -> HTTPResponse {
        requests.append(request)
        guard !results.isEmpty else { throw URLError(.notConnectedToInternet) }
        switch results.removeFirst() {
        case let .response(response): return response
        case let .failure(code): throw URLError(code)
        }
    }
}

actor MockAPIKeyStore: APIKeyStoring {
    private(set) var value: String?
    private(set) var readCount = 0
    private(set) var saveCount = 0
    private(set) var deleteCount = 0
    private var remainingDeleteFailures: Int
    init(_ value: String? = nil, deleteFailures: Int = 0) {
        self.value = value
        self.remainingDeleteFailures = deleteFailures
    }
    func read() async throws -> String? {
        readCount += 1
        return value
    }
    func save(_ value: String) async throws {
        saveCount += 1
        self.value = value
    }
    func delete() async throws {
        deleteCount += 1
        if remainingDeleteFailures > 0 {
            remainingDeleteFailures -= 1
            throw KeychainError.deletionVerificationFailed
        }
        value = nil
    }
}

actor MockBalanceCache: BalanceCaching {
    private(set) var value: CachedBalance?
    private(set) var saveCount = 0
    private(set) var clearCount = 0

    init(_ value: CachedBalance? = nil) { self.value = value }

    func load() async -> CachedBalance? { value }
    func save(_ value: CachedBalance) async {
        saveCount += 1
        self.value = value
    }
    func clear() async {
        clearCount += 1
        value = nil
    }
}

func json(_ string: String) -> Data { Data(string.utf8) }

func flashcatPage(
    pageID: Int64 = 6_410_630_422_455,
    domain: String = "status.deepseek.com",
    apiStatus: String? = nil,
    chatStatus: String? = nil,
    activeChanges: String = "[]"
) -> Data {
    func statusField(_ value: String?) -> String { value.map { ",\"status\":\"\($0)\"" } ?? "" }
    let payload = """
    {"page":{"page_id":\(pageID),"name":"DeepSeek","custom_domain":"\(domain)","components":[
      {"component_id":"api-pro","name":"DeepSeek V4 Pro API服务(API Service)"\(statusField(apiStatus))},
      {"component_id":"api-flash","name":"DeepSeek V4 Flash API服务(API Service)","status":"operational"},
      {"component_id":"chat-instant","section_id":"chat","name":"快速模式(Instant Mode)"\(statusField(chatStatus))},
      {"component_id":"chat-search","section_id":"chat","name":"搜索服务(Search Service)","status":"operational"}],
     "sections":[{"section_id":"chat","name":"对话服务(Chat Service)"}]},
     "active_changes":\(activeChanges)}
    """
    let encoded = try! JSONSerialization.data(withJSONObject: ["1e:[\"$\",\"component\",null,{\"initialData\":\(payload)}]"])
    let literal = String(decoding: encoded, as: UTF8.self).dropFirst().dropLast()
    return Data("<html><script>self.__next_f.push([1,\(literal)])</script></html>".utf8)
}

@MainActor
func waitUntil(_ message: String, _ predicate: @escaping @MainActor () -> Bool) async {
    for _ in 0..<200 {
        if predicate() { return }
        try? await Task.sleep(nanoseconds: 1_000_000)
    }
    check(false, message)
}

func expectBalanceError(_ status: Int, _ expected: BalanceAPIError) async {
    let client = MockHTTPClient([HTTPResponse(data: Data(), statusCode: status)])
    do {
        _ = try await DeepSeekBalanceAPI.fetch(using: client, apiKey: "test-api-key")
        check(false, "HTTP \(status) should throw")
    } catch let error as BalanceAPIError {
        check(error == expected, "HTTP \(status) maps to \(expected)")
    } catch {
        check(false, "HTTP \(status) threw unexpected error")
    }
}

@main
@MainActor
enum PeakEngineTests {
    static func main() async {
        check(PeakEngine.phase(at: beijingDate(8, 59, 59)) == .offPeak, "08:59:59 is off-peak")
        check(PeakEngine.phase(at: beijingDate(9, 0)) == .peak, "09:00 is peak")
        check(PeakEngine.phase(at: beijingDate(11, 59, 59)) == .peak, "11:59:59 is peak")
        check(PeakEngine.phase(at: beijingDate(12, 0)) == .offPeak, "12:00 is off-peak")
        check(PeakEngine.phase(at: beijingDate(13, 59, 59)) == .offPeak, "13:59:59 is off-peak")
        check(PeakEngine.phase(at: beijingDate(14, 0)) == .peak, "14:00 is peak")
        check(PeakEngine.phase(at: beijingDate(17, 59, 59)) == .peak, "17:59:59 is peak")
        check(PeakEngine.phase(at: beijingDate(18, 0)) == .offPeak, "18:00 is off-peak")

        let evening = beijingDate(23, 30)
        let nextMorning = beijingDate(9, 0, dayOffset: 1)
        check(PeakEngine.nextBoundary(after: evening) == nextMorning, "evening transitions next day at 09:00")
        check(PeakEngine.snapshot(at: evening).secondsToNextBoundary == 9.5 * 3600, "evening countdown is 9.5 hours")

        let entries = WidgetTimelinePlan.entries(from: beijingDate(8, 0), days: 1)
        check(entries.map { $0.date } == [
            beijingDate(8, 0), beijingDate(9, 0), beijingDate(12, 0), beijingDate(14, 0), beijingDate(18, 0)
        ], "widget entries contain only the four daily transitions")
        check(PeakEngine.countdownText(3_661) == "1:01:01", "countdown formats hours")
        check(PeakEngine.countdownText(59) == "00:59", "countdown formats under one hour")

        let offPeakPlans = TimeDetectNotificationPlan.offPeakPlans()
        check(offPeakPlans.map { [$0.hour, $0.minute] } == [[12, 0], [18, 0]], "off-peak notifications run at 12:00 and 18:00")
        check(offPeakPlans.map(\.identifier) == ["timedetect.offpeak.1200", "timedetect.offpeak.1800"], "off-peak identifiers are stable")
        check(offPeakPlans.allSatisfy { $0.dateComponents.timeZone == PeakEngine.beijingTimeZone }, "off-peak plans use Beijing timezone")

        let expectedAdvanceTimes: [Int: [[Int]]] = [
            5: [[11, 55], [17, 55]],
            10: [[11, 50], [17, 50]],
            15: [[11, 45], [17, 45]],
            30: [[11, 30], [17, 30]]
        ]
        for minutes in [5, 10, 15, 30] {
            let plans = TimeDetectNotificationPlan.advancePlans(minutes: minutes)
            check(plans.map { [$0.hour, $0.minute] } == expectedAdvanceTimes[minutes]!, "advance \(minutes)-minute notification times")
            check(plans.allSatisfy { $0.dateComponents.timeZone == PeakEngine.beijingTimeZone }, "advance \(minutes)-minute plans use Beijing timezone")
        }
        check(TimeDetectNotificationPlan.advancePlans(minutes: 10).map(\.identifier) == [
            "timedetect.advance.1200", "timedetect.advance.1800"
        ], "advance identifiers are stable across minute settings")

        check(FloatingWindowMode.defaultMode == .desktop, "floating window defaults to desktop mode")
        check(FloatingWindowMode(storedRawValue: nil) == .desktop, "missing stored mode falls back to desktop")
        check(FloatingWindowMode(storedRawValue: "invalid") == .desktop, "invalid stored mode falls back to desktop")
        let suiteName = "TimeDetect.FloatingWindowModeTests"
        let modeDefaults = UserDefaults(suiteName: suiteName)!
        modeDefaults.removePersistentDomain(forName: suiteName)
        modeDefaults.set(FloatingWindowMode.alwaysOnTop.rawValue, forKey: "floatingWindowMode")
        check(FloatingWindowMode(storedRawValue: modeDefaults.string(forKey: "floatingWindowMode")) == .alwaysOnTop, "floating mode persists through UserDefaults raw value")
        modeDefaults.removePersistentDomain(forName: suiteName)

        check(ServiceHealth.overallIndicator("none") == .operational, "none maps to operational")
        check(ServiceHealth.overallIndicator("minor") == .degraded, "minor maps to degraded")
        check(ServiceHealth.overallIndicator("major") == .partialOutage, "major maps to partial outage")
        check(ServiceHealth.overallIndicator("critical") == .majorOutage, "critical maps to major outage")
        check(ServiceHealth.overallIndicator("future") == .unknown, "unknown indicator stays unknown")
        check(ServiceHealth.componentStatus("operational") == .operational, "component operational")
        check(ServiceHealth.componentStatus("degraded_performance") == .degraded, "component degraded")
        check(ServiceHealth.componentStatus("partial_outage") == .partialOutage, "component partial outage")
        check(ServiceHealth.componentStatus("major_outage") == .majorOutage, "component major outage")
        check(ServiceHealth.componentStatus("under_maintenance") == .maintenance, "component maintenance")
        check(ServiceHealth.componentStatus("future") == .unknown, "unknown component stays unknown")
        check(StatuspageParser.matches("API Service", kind: .api), "API component name matches")
        check(StatuspageParser.matches("Web Chat Service", kind: .webChat), "Web Chat component name matches")

        let statusJSON = json("""
        {"status":{"indicator":"minor"},"components":[
          {"name":"API Service","status":"degraded_performance"},
          {"name":"Web Chat Service","status":"operational"}],
         "incidents":[{"id":"i1","name":"API performance issue","status":"investigating"}]}
        """)
        let statusSnapshot = try! StatuspageParser.summary(from: statusJSON)
        check(statusSnapshot.overall == .degraded, "summary parses overall status")
        check(statusSnapshot.services.first { $0.kind == .api }?.health == .degraded, "summary finds API by name")
        check(statusSnapshot.services.first { $0.kind == .webChat }?.health == .operational, "summary finds web chat by name")
        check(statusSnapshot.incidents.first?.statusText == "正在调查", "incident status is localized")

        let flashcatIncident = """
        [{"change_id":123,"title":"API degraded","status":"investigating","type":"incident",
          "affected_components":[{"component_id":"api-pro","name":"API Service","status":"full_outage"}]}]
        """
        let flashcatSnapshot = try! StatuspageParser.flashcatPage(from: flashcatPage(
            apiStatus: "degraded",
            chatStatus: "partial_outage",
            activeChanges: flashcatIncident
        ))
        check(flashcatSnapshot.overall == .majorOutage, "Flashcat overall uses worst current component status")
        check(flashcatSnapshot.services.first { $0.kind == .api }?.health == .majorOutage, "Flashcat aggregates API components and active impact")
        check(flashcatSnapshot.services.first { $0.kind == .webChat }?.health == .partialOutage, "Flashcat aggregates chat section components")
        check(flashcatSnapshot.incidents == [ServiceIncident(id: "123", name: "API degraded", status: "investigating")], "Flashcat active incident parses")
        let operationalFlashcat = try! StatuspageParser.flashcatPage(from: flashcatPage())
        check(operationalFlashcat.overall == .operational, "omitted Flashcat component status means operational when no active impact exists")
        do {
            _ = try StatuspageParser.flashcatPage(from: flashcatPage(pageID: 1))
            check(false, "unexpected Flashcat tenant should fail")
        } catch let error as FlashcatParseError {
            check(error == .unexpectedPage, "Flashcat tenant identity is validated")
        } catch { check(false, "unexpected Flashcat tenant threw wrong error") }
        do {
            _ = try StatuspageParser.flashcatPage(from: json("<html>Everything is running smoothly</html>"))
            check(false, "plain operational HTML should fail")
        } catch { check(true, "plain HTML cannot be mistaken for operational status") }

        let cny = try! BalanceParser.parse(json("""
        {"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"110.00","granted_balance":"10.00","topped_up_balance":"100.00"}]}
        """))
        check(cny.isAvailable && cny.balances.count == 1, "single CNY parses")
        check(cny.balances[0].total == Decimal(string: "110.00")!, "total balance uses Decimal")
        check(cny.balances[0].granted == Decimal(string: "10.00")!, "granted balance parses")
        check(cny.balances[0].toppedUp == Decimal(string: "100.00")!, "topped-up balance parses")
        check(cny.balances[0].totalText == "¥110.00", "CNY formats with yuan symbol")

        let usd = try! BalanceParser.parse(json("""
        {"is_available":true,"balance_infos":[{"currency":"USD","total_balance":"16.42","granted_balance":"1.42","topped_up_balance":"15.00"}]}
        """))
        check(usd.balances[0].totalText == "$16.42", "USD formats with dollar symbol")
        let multi = try! BalanceParser.parse(json("""
        {"is_available":false,"balance_infos":[
          {"currency":"CNY","total_balance":"0.00","granted_balance":"0.00","topped_up_balance":"0.00"},
          {"currency":"USD","total_balance":"2.50","granted_balance":"0.50","topped_up_balance":"2.00"}]}
        """))
        check(!multi.isAvailable && multi.balances.count == 2, "multiple currencies and unavailable flag parse")
        do {
            _ = try BalanceParser.parse(json("{\"is_available\":true,\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"bad\",\"granted_balance\":\"0\",\"topped_up_balance\":\"0\"}]}"))
            check(false, "invalid amount should fail")
        } catch { check(true, "invalid amount rejected") }

        await expectBalanceError(401, .invalidKey)
        await expectBalanceError(402, .insufficientBalance)
        await expectBalanceError(429, .rateLimited)
        await expectBalanceError(500, .serviceUnavailable(500))
        await expectBalanceError(503, .serviceUnavailable(503))
        let malformedClient = MockHTTPClient([HTTPResponse(data: json("{}"), statusCode: 200)])
        do {
            _ = try await DeepSeekBalanceAPI.fetch(using: malformedClient, apiKey: "test-api-key")
            check(false, "malformed 200 should fail")
        } catch let error as BalanceAPIError {
            check(error == .malformedResponse, "malformed 200 maps correctly")
        } catch { check(false, "malformed 200 threw unexpected error") }

        let requestClient = MockHTTPClient([HTTPResponse(data: json("{\"is_available\":true,\"balance_infos\":[]}"), statusCode: 200)])
        _ = try! await DeepSeekBalanceAPI.fetch(using: requestClient, apiKey: "test-api-key")
        let balanceRequests = await requestClient.requests
        check(balanceRequests.count == 1, "balance sends one request")
        check(balanceRequests[0].url?.host == "api.deepseek.com", "key is sent only to official API host")
        check(balanceRequests[0].value(forHTTPHeaderField: "Authorization") == "Bearer test-api-key", "bearer header is formed")

        let keyStore = MockAPIKeyStore()
        try! await keyStore.save("test-api-key")
        let savedMockKey = try! await keyStore.read()
        check(savedMockKey == "test-api-key", "mock key store supports save/read")
        try! await keyStore.delete()
        let deletedMockKey = try! await keyStore.read()
        check(deletedMockKey == nil, "mock key store supports delete")

        let statusManagerClient = MockHTTPClient([HTTPResponse(data: flashcatPage(apiStatus: "degraded"), statusCode: 200)])
        let statusManager = DeepSeekStatusManager(client: statusManagerClient)
        statusManager.refresh()
        statusManager.refresh()
        await waitUntil("status manager should finish") { !statusManager.isRefreshing }
        check(statusManager.snapshot?.overall == .degraded, "status manager publishes successful snapshot")
        check(statusManager.lastUpdated != nil, "status manager records successful update time")
        let statusManagerRequestCount = await statusManagerClient.requests.count
        check(statusManagerRequestCount == 1, "status manager deduplicates in-flight refreshes")
        let statusRequests = await statusManagerClient.requests
        check(statusRequests.first?.url?.absoluteString == "https://statuspage.flashcat.cloud/deepseek", "status manager uses official Flashcat vendor slug")
        statusManager.stop()

        let legacyFallbackClient = MockHTTPClient(results: [
            .failure(.cannotConnectToHost),
            .response(HTTPResponse(data: statusJSON, statusCode: 200))
        ])
        let legacySnapshot = try! await DeepSeekStatusManager.fetch(using: legacyFallbackClient)
        check(legacySnapshot.overall == .degraded, "legacy Statuspage summary remains a fallback")
        let fallbackRequests = await legacyFallbackClient.requests
        check(fallbackRequests.count == 2, "status fallbacks are sequential and bounded")
        check(fallbackRequests[1].url?.absoluteString == "https://status.deepseek.com/api/v2/summary.json", "fallback requests legacy official summary")

        let managerKeyStore = MockAPIKeyStore()
        let managerBalanceCache = MockBalanceCache()
        let balanceManagerClient = MockHTTPClient([
            HTTPResponse(data: json("{\"is_available\":true,\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"8.00\",\"granted_balance\":\"3.00\",\"topped_up_balance\":\"5.00\"}]}"), statusCode: 200)
        ])
        let balanceManager = DeepSeekBalanceManager(
            client: balanceManagerClient,
            keyStore: managerKeyStore,
            balanceCache: managerBalanceCache
        )
        balanceManager.saveAndValidate("test-api-key")
        await waitUntil("balance manager should save configured key") {
            balanceManager.state == .available && !balanceManager.isRefreshing
        }
        check(balanceManager.isConfigured, "balance manager accepts a user-supplied key")
        check(balanceManager.keySuffix == "-key", "balance manager exposes only four-character suffix")
        check(balanceManager.balance?.balances.first?.total == Decimal(8), "balance manager publishes balance")
        await waitUntil("successful balance should be persisted") {
            balanceManager.lastUpdated != nil
        }
        let cachedAfterSave = await managerBalanceCache.value
        check(cachedAfterSave?.snapshot.balances.first?.total == Decimal(8), "successful balance is persisted")
        balanceManager.stop()

        // 模拟彻底退出 App 后创建全新的 Manager：Key 来自 Keychain，余额来自持久缓存。
        let restartedClient = MockHTTPClient([])
        let restartedManager = DeepSeekBalanceManager(
            client: restartedClient,
            keyStore: managerKeyStore,
            balanceCache: managerBalanceCache
        )
        restartedManager.start()
        await waitUntil("restarted balance manager should restore and finish refreshing") {
            restartedManager.isConfigured && !restartedManager.isRefreshing && restartedManager.state == .networkUnavailable
        }
        let startupReadCount = await managerKeyStore.readCount
        check(startupReadCount >= 1, "restarted balance manager reads the stored Keychain key")
        check(restartedManager.keySuffix == "-key", "restarted balance manager restores key metadata")
        check(restartedManager.balance?.balances.first?.total == Decimal(8), "restarted balance manager retains cached balance while offline")

        restartedManager.deleteKey()
        await waitUntil("balance manager should finish deleting key") { !restartedManager.isDeletingKey }
        let managerDeletedKey = try! await managerKeyStore.read()
        let deletedCachedBalance = await managerBalanceCache.value
        check(managerDeletedKey == nil, "balance manager permanently deletes the stored key")
        check(deletedCachedBalance == nil, "balance manager permanently deletes the persisted balance")
        check(!restartedManager.isConfigured, "balance manager clears the in-memory key")
        check(restartedManager.keySuffix == nil, "balance manager clears the key suffix")
        check(restartedManager.balance == nil, "balance manager clears cached balance after key deletion")
        check(restartedManager.lastUpdated == nil, "balance manager clears balance update time after key deletion")
        check(restartedManager.state == .unconfigured, "balance manager returns to unconfigured after key deletion")
        let requestsAfterDeletion = await restartedClient.requests.count
        restartedManager.refresh(force: true)
        try? await Task.sleep(nanoseconds: 5_000_000)
        let finalRequestCount = await restartedClient.requests.count
        check(finalRequestCount == requestsAfterDeletion, "deleted key cannot be used for later requests")
        restartedManager.stop()

        let retryKeyStore = MockAPIKeyStore("retry-test-key", deleteFailures: 1)
        let retryManager = DeepSeekBalanceManager(
            client: MockHTTPClient([]),
            keyStore: retryKeyStore,
            balanceCache: MockBalanceCache()
        )
        retryManager.deleteKey()
        await waitUntil("failed deletion should finish") { !retryManager.isDeletingKey }
        check(retryManager.state == .keychainError, "failed deletion reports a keychain error")
        check(retryManager.canRetryKeyDeletion, "failed deletion remains retryable")
        check(!retryManager.isConfigured, "failed deletion does not restore the key to memory")
        let retainedKey = try! await retryKeyStore.read()
        check(retainedKey == "retry-test-key", "failed deletion can leave the persistent key for retry")
        retryManager.retryKeyDeletion()
        await waitUntil("retried deletion should finish") { !retryManager.isDeletingKey }
        let retriedDeletedKey = try! await retryKeyStore.read()
        check(retriedDeletedKey == nil, "retried deletion removes the persistent key")
        check(!retryManager.canRetryKeyDeletion, "successful retry clears retry state")
        check(retryManager.state == .unconfigured, "successful retry returns to unconfigured state")
        retryManager.stop()

        print("TimeDetect tests passed")
    }
}