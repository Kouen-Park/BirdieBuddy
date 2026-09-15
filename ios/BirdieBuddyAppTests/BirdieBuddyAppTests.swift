import XCTest
@testable import BirdieBuddyApp

final class BirdieBuddyAppTests: XCTestCase {
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
}
