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
        let persistence = try RoundPersistenceStore(directoryURL: directory, inMemory: true)
        let first = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 1, expectedUpdatedAt: nil, payload: Data([1]))
        let latest = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 2, expectedUpdatedAt: nil, payload: Data([2]))
        try await persistence.enqueue(userId: 7, write: first)
        try await persistence.enqueue(userId: 7, write: latest)
        let firstUserWrites = try await persistence.pending(userId: 7)
        let secondUserWrites = try await persistence.pending(userId: 8)
        XCTAssertEqual(firstUserWrites, [latest])
        XCTAssertTrue(secondUserWrites.isEmpty)
    }

    func testOutboxHydratesBeforeEnqueueFromANewStoreInstance() async throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        let first = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 1, expectedUpdatedAt: nil, payload: Data([1]))
        let second = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 2, revision: 2, expectedUpdatedAt: nil, payload: Data([2]))

        let persistence = try RoundPersistenceStore(directoryURL: directory, inMemory: true)
        try await persistence.enqueue(userId: 7, write: first)
        try await persistence.enqueue(userId: 7, write: second)

        let writes = try await persistence.pending(userId: 7)
        XCTAssertEqual(Set(writes.map(\.id)), Set([first.id, second.id]))
    }

    func testOutboxHydratesBeforeAcknowledgeAndPreservesOtherWrites() async throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        let first = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 1, expectedUpdatedAt: nil, payload: Data([1]))
        let second = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 2, revision: 2, expectedUpdatedAt: nil, payload: Data([2]))
        let persistence = try RoundPersistenceStore(directoryURL: directory, inMemory: true)
        try await persistence.enqueue(userId: 7, write: first)
        try await persistence.enqueue(userId: 7, write: second)
        try await persistence.acknowledge(userId: 7, id: first.id)

        let writes = try await persistence.pending(userId: 7)
        XCTAssertEqual(writes, [second])
    }

    func testLegacyJSONMigratesOnceAndArchivesOriginal() async throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let older = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 1,
            expectedUpdatedAt: nil, payload: Data([1]))
        let latest = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 2,
            expectedUpdatedAt: nil, payload: Data([2]))
        let legacyURL = directory.appendingPathComponent("round-outbox-7.json")
        try JSONEncoder.birdieBuddy.encode([older, latest]).write(to: legacyURL)

        let persistence = try RoundPersistenceStore(directoryURL: directory)
        let migrated = try await persistence.pending(userId: 7)

        let migratedWrite = try XCTUnwrap(migrated.first)
        XCTAssertEqual(migrated.count, 1)
        XCTAssertEqual(migratedWrite.id, latest.id)
        XCTAssertEqual(migratedWrite.roundId, latest.roundId)
        XCTAssertEqual(migratedWrite.holeNumber, latest.holeNumber)
        XCTAssertEqual(migratedWrite.revision, latest.revision)
        XCTAssertEqual(migratedWrite.payload, latest.payload)
        XCTAssertEqual(migratedWrite.state, .queued)
        XCTAssertFalse(FileManager.default.fileExists(atPath: legacyURL.path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: legacyURL.path + ".migrated-v1"))
    }

    func testDeletingAccountDataDoesNotDeleteAnotherUsersWrites() async throws {
        let persistence = try RoundPersistenceStore(inMemory: true)
        let first = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 1,
            expectedUpdatedAt: nil, payload: Data([1]))
        let second = PendingHoleWrite(id: UUID(), roundId: 20, holeNumber: 1, revision: 1,
            expectedUpdatedAt: nil, payload: Data([2]))
        try await persistence.enqueue(userId: 7, write: first)
        try await persistence.enqueue(userId: 8, write: second)

        try await persistence.deleteUserData(userId: 7)

        let deletedUserWrites = try await persistence.pending(userId: 7)
        let remainingUserWrites = try await persistence.pending(userId: 8)
        XCTAssertTrue(deletedUserWrites.isEmpty)
        XCTAssertEqual(remainingUserWrites, [second])
    }

    func testConflictPersistsAndServerResolutionClearsWriteAtomically() async throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        let request = HoleUpsertRequest(par: 4, score: 6, putts: 2, gir: false, fairwayHit: true,
            penalty: 0, checkExpected: true, expectedHole: nil)
        let write = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 1,
            expectedUpdatedAt: nil, payload: try JSONEncoder.birdieBuddy.encode(request))
        do {
            let persistence = try RoundPersistenceStore(directoryURL: directory)
            try await persistence.enqueue(userId: 7, write: write)
            try await persistence.saveConflict(userId: 7, write: write, serverHole: makeHole(number: 1, score: 4))
        }

        let relaunched = try RoundPersistenceStore(directoryURL: directory)
        let restoredConflict = try await relaunched.conflict(userId: 7, roundId: 10)
        let conflict = try XCTUnwrap(restoredConflict)
        let server = try await relaunched.useServerValue(userId: 7, conflictId: conflict.id)

        XCTAssertEqual(server?.score, 4)
        let clearedConflict = try await relaunched.conflict(userId: 7, roundId: 10)
        let clearedWrites = try await relaunched.pending(userId: 7, roundId: 10)
        XCTAssertNil(clearedConflict)
        XCTAssertTrue(clearedWrites.isEmpty)
    }

    func testKeepingLocalConflictCreatesForcedHigherRevisionWrite() async throws {
        let persistence = try RoundPersistenceStore(inMemory: true)
        let request = HoleUpsertRequest(par: 4, score: 6, putts: 2, gir: false, fairwayHit: true,
            penalty: 0, checkExpected: true, expectedHole: makeHole(number: 1, score: 4))
        let original = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 4,
            expectedUpdatedAt: nil, payload: try JSONEncoder.birdieBuddy.encode(request))
        try await persistence.enqueue(userId: 7, write: original)
        try await persistence.saveConflict(userId: 7, write: original, serverHole: makeHole(number: 1, score: 5))
        let storedConflict = try await persistence.conflict(userId: 7, roundId: 10)
        let conflict = try XCTUnwrap(storedConflict)

        let storedForced = try await persistence.keepLocalValue(userId: 7, conflictId: conflict.id, revision: 1)
        let forced = try XCTUnwrap(storedForced)
        let decoded = try JSONDecoder.birdieBuddy.decode(HoleUpsertRequest.self, from: forced.payload)

        XCTAssertEqual(forced.revision, 5)
        XCTAssertFalse(decoded.checkExpected)
        XCTAssertNil(decoded.expectedHole)
        let remainingConflict = try await persistence.conflict(userId: 7, roundId: 10)
        XCTAssertNil(remainingConflict)
    }

    func testSyncRetriesNetworkFailureAndAcknowledgesAfterRecovery() async throws {
        let persistence = try RoundPersistenceStore(inMemory: true)
        let session = makeURLSession()
        let store = InMemorySessionStore(session: makeSession(accessToken: "access", refreshToken: "refresh"))
        let api = APIClient(baseURL: URL(string: "https://example.test")!, keychain: store, urlSession: session)
        let coordinator = SyncCoordinator(persistence: persistence, api: api)
        let request = HoleUpsertRequest(par: 4, score: 4, putts: 2, gir: true, fairwayHit: true,
            penalty: 0, checkExpected: true, expectedHole: nil)
        let write = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 1,
            expectedUpdatedAt: nil, payload: try JSONEncoder.birdieBuddy.encode(request))
        try await persistence.enqueue(userId: 7, write: write)
        MockURLProtocol.requestHandler = { _ in throw URLError(.notConnectedToInternet) }

        _ = await coordinator.synchronize(userId: 7, trigger: .manualSave)
        let offlineStatus = try await persistence.status(userId: 7, roundId: 10)
        XCTAssertEqual(offlineStatus, .savedOnDevice)

        MockURLProtocol.requestHandler = { request in Self.response(request, status: 200, json: Self.holeJSON(number: 1)) }
        _ = await coordinator.synchronize(userId: 7, trigger: .networkRestored)
        let recoveredStatus = try await persistence.status(userId: 7, roundId: 10)
        XCTAssertEqual(recoveredStatus, .synced)
    }

    func testExpiredAuthenticationPreservesPendingWriteAfterRefreshFailure() async throws {
        let persistence = try RoundPersistenceStore(inMemory: true)
        let session = makeURLSession()
        let store = InMemorySessionStore(session: makeSession(accessToken: "expired", refreshToken: "expired-refresh"))
        let api = APIClient(baseURL: URL(string: "https://example.test")!, keychain: store, urlSession: session)
        let coordinator = SyncCoordinator(persistence: persistence, api: api)
        let request = HoleUpsertRequest(par: 4, score: 4, putts: 2, gir: true, fairwayHit: true,
            penalty: 0, checkExpected: true, expectedHole: nil)
        let write = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 1,
            expectedUpdatedAt: nil, payload: try JSONEncoder.birdieBuddy.encode(request))
        try await persistence.enqueue(userId: 7, write: write)
        MockURLProtocol.requestHandler = { request in
            Self.response(request, status: 401, json: Self.problemJSON)
        }

        _ = await coordinator.synchronize(userId: 7, trigger: .foreground)

        let status = try await persistence.status(userId: 7, roundId: 10)
        let remaining = try await persistence.pending(userId: 7, roundId: 10)
        XCTAssertEqual(status, .savedOnDevice)
        XCTAssertEqual(remaining.map(\.id), [write.id])
        XCTAssertNil(store.session)
    }

    func testPermanentClientErrorRequiresAttentionInsteadOfRetryingForever() async throws {
        let persistence = try RoundPersistenceStore(inMemory: true)
        let session = makeURLSession()
        let store = InMemorySessionStore(session: makeSession(accessToken: "access", refreshToken: "refresh"))
        let api = APIClient(baseURL: URL(string: "https://example.test")!, keychain: store, urlSession: session)
        let coordinator = SyncCoordinator(persistence: persistence, api: api)
        let request = HoleUpsertRequest(par: 4, score: 4, putts: 2, gir: true, fairwayHit: true,
            penalty: 0, checkExpected: true, expectedHole: nil)
        let write = PendingHoleWrite(id: UUID(), roundId: 10, holeNumber: 1, revision: 1,
            expectedUpdatedAt: nil, payload: try JSONEncoder.birdieBuddy.encode(request))
        try await persistence.enqueue(userId: 7, write: write)
        MockURLProtocol.requestHandler = { request in
            Self.response(request, status: 422, json: #"{"status":422,"title":"Invalid","code":"round.invalid"}"#)
        }

        _ = await coordinator.synchronize(userId: 7, trigger: .manualSave)
        _ = await coordinator.synchronize(userId: 7, trigger: .foreground)

        let status = try await persistence.status(userId: 7, roundId: 10)
        XCTAssertEqual(status, .attentionRequired)
    }

    func testSyncSendsWritesInRevisionOrder() async throws {
        let persistence = try RoundPersistenceStore(inMemory: true)
        let session = makeURLSession()
        let store = InMemorySessionStore(session: makeSession(accessToken: "access", refreshToken: "refresh"))
        let api = APIClient(baseURL: URL(string: "https://example.test")!, keychain: store, urlSession: session)
        let coordinator = SyncCoordinator(persistence: persistence, api: api)
        let request = HoleUpsertRequest(par: 4, score: 4, putts: 2, gir: true, fairwayHit: true,
            penalty: 0, checkExpected: true, expectedHole: nil)
        let requestOrder = RequestOrder()
        for (holeNumber, revision) in [(2, 20), (1, 10)] {
            try await persistence.enqueue(userId: 7, write: PendingHoleWrite(
                id: UUID(), roundId: 10, holeNumber: holeNumber, revision: revision,
                expectedUpdatedAt: nil, payload: try JSONEncoder.birdieBuddy.encode(request)
            ))
        }
        MockURLProtocol.requestHandler = { request in
            let holeNumber = Int(request.url?.lastPathComponent ?? "0") ?? 0
            requestOrder.holeNumbers.append(holeNumber)
            return Self.response(request, status: 200, json: Self.holeJSON(number: holeNumber))
        }

        _ = await coordinator.synchronize(userId: 7, trigger: .networkRestored)

        XCTAssertEqual(requestOrder.holeNumbers, [1, 2])
    }

    func testConflictStopsOnlyItsRoundAndAllowsOtherRoundsToSync() async throws {
        let persistence = try RoundPersistenceStore(inMemory: true)
        let session = makeURLSession()
        let store = InMemorySessionStore(session: makeSession(accessToken: "access", refreshToken: "refresh"))
        let api = APIClient(baseURL: URL(string: "https://example.test")!, keychain: store, urlSession: session)
        let coordinator = SyncCoordinator(persistence: persistence, api: api)
        let request = HoleUpsertRequest(par: 4, score: 5, putts: 2, gir: false, fairwayHit: true,
            penalty: 0, checkExpected: true, expectedHole: nil)
        for roundId in [10, 20] {
            try await persistence.enqueue(userId: 7, write: PendingHoleWrite(
                id: UUID(), roundId: roundId, holeNumber: 1, revision: 1,
                expectedUpdatedAt: nil, payload: try JSONEncoder.birdieBuddy.encode(request)
            ))
        }
        MockURLProtocol.requestHandler = { request in
            switch (request.httpMethod, request.url?.path) {
            case ("PUT", "/api/rounds/10/holes/by-number/1"):
                return Self.response(request, status: 409,
                    json: #"{"status":409,"title":"Conflict","code":"round.hole_conflict"}"#)
            case ("GET", "/api/rounds/10"):
                return Self.response(request, status: 200, json: Self.draftJSON(status: "Draft"))
            case ("PUT", "/api/rounds/20/holes/by-number/1"):
                return Self.response(request, status: 200, json: Self.holeJSON(number: 1))
            default:
                return Self.response(request, status: 404, json: Self.problemJSON)
            }
        }

        let report = await coordinator.synchronize(userId: 7, trigger: .networkRestored)

        let conflictedStatus = try await persistence.status(userId: 7, roundId: 10)
        let otherRoundStatus = try await persistence.status(userId: 7, roundId: 20)
        XCTAssertEqual(report.conflictRoundIds, [10])
        XCTAssertEqual(conflictedStatus, .reviewRequired)
        XCTAssertEqual(otherRoundStatus, .synced)
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
        let offlineStore = try RoundPersistenceStore(directoryURL: directory)
        for holeNumber in 1...18 {
            let request = HoleUpsertRequest(par: 4, score: 4, putts: 2, gir: true, fairwayHit: true,
                penalty: 0, checkExpected: true, expectedHole: nil)
            try await offlineStore.enqueue(userId: 7, write: PendingHoleWrite(
                id: UUID(),
                roundId: draft.id,
                holeNumber: holeNumber,
                revision: holeNumber,
                expectedUpdatedAt: draft.updatedAt,
                payload: try JSONEncoder.birdieBuddy.encode(request)
            ))
        }

        let restoredWrites = try await offlineStore.pending(userId: 7, roundId: draft.id)
        XCTAssertEqual(restoredWrites.count, 18)
        for write in restoredWrites {
            let request = try JSONDecoder.birdieBuddy.decode(HoleUpsertRequest.self, from: write.payload)
            _ = try await api.saveHole(roundId: write.roundId, holeNumber: write.holeNumber, request: request)
            try await offlineStore.acknowledge(userId: 7, id: write.id)
        }
        _ = try await api.complete(roundId: draft.id)

        let remaining = try await offlineStore.pending(userId: 7, roundId: draft.id)
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

private final class RequestOrder {
    var holeNumbers: [Int] = []
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
