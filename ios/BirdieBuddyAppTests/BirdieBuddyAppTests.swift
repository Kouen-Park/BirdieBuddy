import XCTest
@testable import BirdieBuddyApp

final class BirdieBuddyAppTests: XCTestCase {
    override func tearDown() {
        MockURLProtocol.requestHandler = nil
        super.tearDown()
    }

    func testMobileSessionDecodesServerDates() throws {
        let json = #"{"accessToken":"a","refreshToken":"r","accessTokenExpiresAt":"2026-09-16T01:15:00Z","refreshTokenExpiresAt":"2026-10-16T01:00:00Z","user":{"id":7,"email":"golfer@example.test","displayName":"Test Golfer","emailVerified":true}}"#.data(using: .utf8)!
        let session = try JSONDecoder.birdieBuddy.decode(MobileSession.self, from: json)
        XCTAssertEqual(session.user.id, 7)
        XCTAssertTrue(session.user.emailVerified)
    }

    func testOutboxKeepsLatestRevisionPerHoleAndSeparatesUsers() async throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        let firstUser = RoundDraftStore(userId: 7, directoryURL: directory)
        let secondUser = RoundDraftStore(userId: 8, directoryURL: directory)
        let first = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 1, expectedUpdatedAt: nil, payload: Data([1]))
        let latest = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 2, expectedUpdatedAt: nil, payload: Data([2]))
        try await firstUser.enqueue(first)
        try await firstUser.enqueue(latest)
        let firstUserWrites = try await firstUser.load()
        let secondUserWrites = try await secondUser.load()
        XCTAssertEqual(firstUserWrites, [latest])
        XCTAssertTrue(secondUserWrites.isEmpty)
    }

    func testOutboxHydratesBeforeEnqueueFromANewStoreInstance() async throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        let first = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 1, expectedUpdatedAt: nil, payload: Data([1]))
        let second = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 2, revision: 2, expectedUpdatedAt: nil, payload: Data([2]))

        try await RoundDraftStore(userId: 7, directoryURL: directory).enqueue(first)
        try await RoundDraftStore(userId: 7, directoryURL: directory).enqueue(second)

        let writes = try await RoundDraftStore(userId: 7, directoryURL: directory).load()
        XCTAssertEqual(Set(writes.map(\.id)), Set([first.id, second.id]))
    }

    func testOutboxHydratesBeforeAcknowledgeAndPreservesOtherWrites() async throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        let first = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 1, expectedUpdatedAt: nil, payload: Data([1]))
        let second = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 2, revision: 2, expectedUpdatedAt: nil, payload: Data([2]))
        let writer = RoundDraftStore(userId: 7, directoryURL: directory)
        try await writer.enqueue(first)
        try await writer.enqueue(second)

        try await RoundDraftStore(userId: 7, directoryURL: directory).acknowledge(first.id)

        let writes = try await RoundDraftStore(userId: 7, directoryURL: directory).load()
        XCTAssertEqual(writes, [second])
    }

    func testMutableSnapshotKeepsLocallyEditedHole() {
        let original = makeDraft(holes: [makeHole(number: 1, score: 4)])
        var snapshot = MutableRoundSnapshot(draft: original)

        snapshot.apply(makeHole(number: 1, score: 7))

        XCTAssertEqual(snapshot.hole(number: 1)?.score, 7)
        XCTAssertEqual(snapshot.draft.holes.count, 1)
    }

    func testConflictBlocksNavigationAndCompletion() {
        XCTAssertFalse(HoleSaveOutcome.conflict.permitsHoleNavigation)
        XCTAssertFalse(HoleSaveOutcome.conflict.permitsCompletion)
        XCTAssertFalse(HoleSaveOutcome.failed.permitsHoleNavigation)
        XCTAssertTrue(HoleSaveOutcome.queuedOffline.permitsHoleNavigation)
        XCTAssertFalse(HoleSaveOutcome.queuedOffline.permitsCompletion)
        XCTAssertTrue(HoleSaveOutcome.serverSaved.permitsCompletion)
    }

    func testProductionConfigurationRequiresHTTPSAndRejectsLocalhost() throws {
        XCTAssertThrowsError(try AppConfiguration.resolveAPIBaseURL(
            environmentValue: nil,
            bundledValue: "http://localhost:5000",
            allowsInsecureLocalhost: false
        )) { error in
            XCTAssertEqual(error as? AppConfigurationError, .insecureProductionAPIBaseURL)
        }

        let production = try AppConfiguration.resolveAPIBaseURL(
            environmentValue: nil,
            bundledValue: "https://birdiebuddy.onrender.com",
            allowsInsecureLocalhost: false
        )
        XCTAssertEqual(production.absoluteString, "https://birdiebuddy.onrender.com")
    }

    func testAuthenticatedRequestRefreshesExactlyOnce() async throws {
        let store = InMemorySessionStore(session: makeSession(accessToken: "expired", refreshToken: "refresh-1"))
        let session = makeURLSession()
        let counts = RequestCounts()
        MockURLProtocol.requestHandler = { request in
            if request.url?.path == "/api/mobile/auth/refresh" {
                counts.refresh += 1
                return Self.response(request, status: 200, json: Self.sessionJSON(accessToken: "fresh", refreshToken: "refresh-2"))
            }
            if request.url?.path == "/api/courses" {
                counts.protected += 1
                counts.authorizationHeaders.append(request.value(forHTTPHeaderField: "Authorization") ?? "")
                if counts.protected == 1 { return Self.response(request, status: 401, json: Self.problemJSON) }
                return Self.response(request, status: 200, json: "[]")
            }
            return Self.response(request, status: 404, json: Self.problemJSON)
        }
        let api = APIClient(baseURL: URL(string: "https://example.test")!, keychain: store, urlSession: session)

        let courses = try await api.courses()

        XCTAssertTrue(courses.isEmpty)
        XCTAssertEqual(counts.refresh, 1)
        XCTAssertEqual(counts.protected, 2)
        XCTAssertEqual(counts.authorizationHeaders, ["Bearer expired", "Bearer fresh"])
    }

    func testSecondUnauthorizedResponseInvalidatesSessionWithoutLooping() async throws {
        let store = InMemorySessionStore(session: makeSession(accessToken: "expired", refreshToken: "refresh-1"))
        let session = makeURLSession()
        let counts = RequestCounts()
        let invalidations = AsyncCounter()
        MockURLProtocol.requestHandler = { request in
            if request.url?.path == "/api/mobile/auth/refresh" {
                counts.refresh += 1
                return Self.response(request, status: 200, json: Self.sessionJSON(accessToken: "fresh", refreshToken: "refresh-2"))
            }
            counts.protected += 1
            return Self.response(request, status: 401, json: Self.problemJSON)
        }
        let api = APIClient(baseURL: URL(string: "https://example.test")!, keychain: store, urlSession: session)
        await api.setSessionInvalidationHandler { await invalidations.increment() }

        do {
            let _: [CourseSummary] = try await api.courses()
            XCTFail("Expected the second unauthorized response to fail.")
        } catch let problem as ApiProblem {
            XCTAssertEqual(problem.status, 401)
        }

        let invalidationCount = await invalidations.value
        XCTAssertEqual(counts.refresh, 1)
        XCTAssertEqual(counts.protected, 2)
        XCTAssertEqual(invalidationCount, 1)
        XCTAssertNil(store.session)
        let currentUser = await api.currentUser()
        XCTAssertNil(currentUser)
    }

    func testSessionRestoreKeepsCachedUserWhenOffline() async {
        let cached = makeSession(accessToken: "cached", refreshToken: "refresh")
        let store = InMemorySessionStore(session: cached)
        let session = makeURLSession()
        MockURLProtocol.requestHandler = { _ in throw URLError(.notConnectedToInternet) }
        let api = APIClient(baseURL: URL(string: "https://example.test")!, keychain: store, urlSession: session)

        let restored = await api.restoreSession()

        XCTAssertEqual(restored, cached.user)
        XCTAssertEqual(store.session, cached)
    }

    func testSessionRestoreKeepsCachedUserDuringTransientServerFailure() async {
        let cached = makeSession(accessToken: "cached", refreshToken: "refresh")
        let store = InMemorySessionStore(session: cached)
        let session = makeURLSession()
        MockURLProtocol.requestHandler = { request in
            Self.response(request, status: 503, json: #"{"status":503,"title":"Unavailable","code":"server.unavailable"}"#)
        }
        let api = APIClient(baseURL: URL(string: "https://example.test")!, keychain: store, urlSession: session)

        let restored = await api.restoreSession()

        XCTAssertEqual(restored, cached.user)
        XCTAssertEqual(store.session, cached)
    }

    func testCourseToEighteenHoleOfflineRelaunchAndSyncJourney() async throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        let store = InMemorySessionStore(session: makeSession(accessToken: "access", refreshToken: "refresh"))
        let session = makeURLSession()
        let counts = RequestCounts()
        MockURLProtocol.requestHandler = { request in
            switch (request.httpMethod, request.url?.path) {
            case ("GET", "/api/courses"):
                return Self.response(request, status: 200, json: #"[{"id":3,"name":"Test Course","location":"Auckland"}]"#)
            case ("POST", "/api/rounds/drafts"):
                return Self.response(request, status: 200, json: Self.draftJSON(status: "Draft"))
            case ("PUT", let path?) where path.contains("/api/rounds/10/holes/by-number/"):
                counts.protected += 1
                let number = Int(path.split(separator: "/").last ?? "0") ?? 0
                return Self.response(request, status: 200, json: Self.holeJSON(number: number))
            case ("POST", "/api/rounds/10/complete"):
                return Self.response(request, status: 200, json: Self.draftJSON(status: "Completed"))
            default:
                return Self.response(request, status: 404, json: Self.problemJSON)
            }
        }
        let api = APIClient(baseURL: URL(string: "https://example.test")!, keychain: store, urlSession: session)

        let courses = try await api.courses()
        let selectedCourse = try XCTUnwrap(courses.first)
        let draft = try await api.startDraft(courseId: selectedCourse.id, teeId: 4)
        let offlineStore = RoundDraftStore(userId: 7, directoryURL: directory)
        for holeNumber in 1...18 {
            let request = HoleUpsertRequest(par: 4, score: 4, putts: 2, gir: true, fairwayHit: true,
                penalty: 0, checkExpected: true, expectedHole: nil)
            try await offlineStore.enqueue(PendingHoleWrite(
                id: UUID(),
                roundId: draft.id,
                holeNumber: holeNumber,
                revision: holeNumber,
                expectedUpdatedAt: draft.updatedAt,
                payload: try JSONEncoder.birdieBuddy.encode(request)
            ))
        }

        let relaunchedStore = RoundDraftStore(userId: 7, directoryURL: directory)
        let restoredWrites = try await relaunchedStore.pending(for: draft.id)
        XCTAssertEqual(restoredWrites.count, 18)
        for write in restoredWrites {
            let request = try JSONDecoder.birdieBuddy.decode(HoleUpsertRequest.self, from: write.payload)
            _ = try await api.saveHole(roundId: write.roundId, holeNumber: write.holeNumber, request: request)
            try await relaunchedStore.acknowledge(write.id)
        }
        _ = try await api.complete(roundId: draft.id)

        let finalStore = RoundDraftStore(userId: 7, directoryURL: directory)
        let remaining = try await finalStore.pending(for: draft.id)
        XCTAssertEqual(selectedCourse.id, 3)
        XCTAssertEqual(draft.expectedHoles, 18)
        XCTAssertEqual(counts.protected, 18)
        XCTAssertTrue(remaining.isEmpty)
    }

    private func makeDraft(holes: [Hole]) -> RoundDraft {
        RoundDraft(id: 10, courseId: 3, courseName: "Test Course", date: "2026-09-16", courseTeeId: 4,
            tee: "Blue", holes: holes, status: "Draft", currentHole: 1, expectedHoles: 18, updatedAt: nil)
    }

    private func makeHole(number: Int, score: Int) -> Hole {
        Hole(id: number, holeNumber: number, par: 4, score: score, putts: 2, gir: false, fairwayHit: false, penalty: 0)
    }

    private func makeSession(accessToken: String, refreshToken: String) -> MobileSession {
        MobileSession(
            accessToken: accessToken,
            refreshToken: refreshToken,
            accessTokenExpiresAt: Date(timeIntervalSince1970: 2_000_000_000),
            refreshTokenExpiresAt: Date(timeIntervalSince1970: 2_100_000_000),
            user: CurrentUser(id: 7, email: "golfer@example.test", displayName: "Test Golfer", emailVerified: true)
        )
    }

    private func makeURLSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [MockURLProtocol.self]
        return URLSession(configuration: configuration)
    }

    private static func response(_ request: URLRequest, status: Int, json: String) -> (HTTPURLResponse, Data) {
        let response = HTTPURLResponse(url: request.url!, statusCode: status, httpVersion: nil,
            headerFields: ["Content-Type": "application/json"])!
        return (response, Data(json.utf8))
    }

    private static func sessionJSON(accessToken: String, refreshToken: String) -> String {
        #"{"accessToken":"\#(accessToken)","refreshToken":"\#(refreshToken)","accessTokenExpiresAt":"2033-05-18T03:33:20Z","refreshTokenExpiresAt":"2036-07-18T13:20:00Z","user":{"id":7,"email":"golfer@example.test","displayName":"Test Golfer","emailVerified":true}}"#
    }

    private static func draftJSON(status: String) -> String {
        #"{"id":10,"courseId":3,"courseName":"Test Course","date":"2026-09-16","courseTeeId":4,"tee":"Blue","holes":[],"status":"\#(status)","currentHole":1,"expectedHoles":18,"updatedAt":null}"#
    }

    private static func holeJSON(number: Int) -> String {
        #"{"id":\#(number),"holeNumber":\#(number),"par":4,"score":4,"putts":2,"gir":true,"fairwayHit":true,"penalty":0}"#
    }

    private static let problemJSON = #"{"status":401,"title":"Unauthorized","detail":"Session expired","code":"auth.invalid"}"#
}

private final class InMemorySessionStore: SessionStoring {
    var session: MobileSession?

    init(session: MobileSession?) { self.session = session }
    func save(_ session: MobileSession) throws { self.session = session }
    func load() throws -> MobileSession? { session }
    func remove() { session = nil }
}

private final class RequestCounts {
    var protected = 0
    var refresh = 0
    var authorizationHeaders: [String] = []
}

private actor AsyncCounter {
    private(set) var value = 0
    func increment() { value += 1 }
}

private final class MockURLProtocol: URLProtocol {
    static var requestHandler: ((URLRequest) throws -> (HTTPURLResponse, Data))?

    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        guard let handler = Self.requestHandler else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        do {
            let (response, data) = try handler(request)
            client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            client?.urlProtocol(self, didLoad: data)
            client?.urlProtocolDidFinishLoading(self)
        } catch {
            client?.urlProtocol(self, didFailWithError: error)
        }
    }

    override func stopLoading() {}
}
