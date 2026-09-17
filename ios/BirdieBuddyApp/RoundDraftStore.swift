import Foundation
import SwiftData

enum SyncRecordState: String, Codable, CaseIterable {
    case queued, syncing, conflict, requiresAttention
}

enum SyncTrigger: String, Sendable {
    case sessionRestore, reauthentication, networkRestored, foreground, liveRound, manualSave, conflictResolution
}

enum RoundSyncStatus: String, Equatable, Sendable {
    case synced, savedOnDevice, syncing, reviewRequired, attentionRequired

    var label: String {
        switch self {
        case .synced: "Synced"
        case .savedOnDevice: "Saved on device"
        case .syncing: "Syncing"
        case .reviewRequired: "Review required"
        case .attentionRequired: "Action required"
        }
    }
}

struct PendingHoleWrite: Codable, Equatable, Identifiable, Sendable {
    let id: UUID
    let roundId: Int
    let holeNumber: Int
    let revision: Int
    let expectedUpdatedAt: Date?
    let payload: Data
    var serverSnapshot: Data? = nil
    var createdAt: Date = .now
    var state: SyncRecordState = .queued

    private enum CodingKeys: String, CodingKey {
        case id, roundId, holeNumber, revision, expectedUpdatedAt, payload, serverSnapshot, createdAt, state
    }

    init(id: UUID, roundId: Int, holeNumber: Int, revision: Int, expectedUpdatedAt: Date?, payload: Data,
         serverSnapshot: Data? = nil, createdAt: Date = .now, state: SyncRecordState = .queued) {
        self.id = id
        self.roundId = roundId
        self.holeNumber = holeNumber
        self.revision = revision
        self.expectedUpdatedAt = expectedUpdatedAt
        self.payload = payload
        self.serverSnapshot = serverSnapshot
        self.createdAt = createdAt
        self.state = state
    }

    init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        id = try values.decode(UUID.self, forKey: .id)
        roundId = try values.decode(Int.self, forKey: .roundId)
        holeNumber = try values.decode(Int.self, forKey: .holeNumber)
        revision = try values.decode(Int.self, forKey: .revision)
        expectedUpdatedAt = try values.decodeIfPresent(Date.self, forKey: .expectedUpdatedAt)
        payload = try values.decode(Data.self, forKey: .payload)
        serverSnapshot = try values.decodeIfPresent(Data.self, forKey: .serverSnapshot)
        createdAt = try values.decodeIfPresent(Date.self, forKey: .createdAt) ?? .now
        state = try values.decodeIfPresent(SyncRecordState.self, forKey: .state) ?? .queued
    }
}

struct PersistedRoundConflict: Identifiable, Equatable, Sendable {
    let id: UUID
    let userId: Int
    let roundId: Int
    let holeNumber: Int
    let localPayload: Data
    let serverHolePayload: Data?
    let pendingWriteId: UUID
    let discoveredAt: Date
}

@Model
final class PendingHoleWriteRecord {
    @Attribute(.unique) var id: UUID
    var userId: Int
    var roundId: Int
    var holeNumber: Int
    var revision: Int
    var expectedUpdatedAt: Date?
    var payload: Data
    var serverSnapshot: Data?
    var createdAt: Date
    var stateRawValue: String

    init(userId: Int, write: PendingHoleWrite) {
        id = write.id
        self.userId = userId
        roundId = write.roundId
        holeNumber = write.holeNumber
        revision = write.revision
        expectedUpdatedAt = write.expectedUpdatedAt
        payload = write.payload
        serverSnapshot = write.serverSnapshot
        createdAt = write.createdAt
        stateRawValue = write.state.rawValue
    }

    var value: PendingHoleWrite {
        PendingHoleWrite(id: id, roundId: roundId, holeNumber: holeNumber, revision: revision,
            expectedUpdatedAt: expectedUpdatedAt, payload: payload, serverSnapshot: serverSnapshot,
            createdAt: createdAt, state: SyncRecordState(rawValue: stateRawValue) ?? .queued)
    }
}

@Model
final class RoundConflictRecord {
    @Attribute(.unique) var id: UUID
    var userId: Int
    var roundId: Int
    var holeNumber: Int
    var localPayload: Data
    var serverHolePayload: Data?
    var pendingWriteId: UUID
    var discoveredAt: Date

    init(userId: Int, write: PendingHoleWrite, serverHolePayload: Data?) {
        id = UUID()
        self.userId = userId
        roundId = write.roundId
        holeNumber = write.holeNumber
        localPayload = write.payload
        self.serverHolePayload = serverHolePayload
        pendingWriteId = write.id
        discoveredAt = .now
    }

    var value: PersistedRoundConflict {
        PersistedRoundConflict(id: id, userId: userId, roundId: roundId, holeNumber: holeNumber,
            localPayload: localPayload, serverHolePayload: serverHolePayload,
            pendingWriteId: pendingWriteId, discoveredAt: discoveredAt)
    }
}

actor RoundPersistenceStore: ModelActor {
    nonisolated let modelContainer: ModelContainer
    nonisolated let modelExecutor: any ModelExecutor
    private let fileManager: FileManager
    private let legacyDirectoryURL: URL
    private var migratedUsers: Set<Int> = []

    init(directoryURL: URL? = nil, inMemory: Bool = false, fileManager: FileManager = .default) throws {
        self.fileManager = fileManager
        let root = directoryURL ?? fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        legacyDirectoryURL = root
        let schema = Schema([PendingHoleWriteRecord.self, RoundConflictRecord.self])
        let configuration: ModelConfiguration
        if inMemory {
            configuration = ModelConfiguration(schema: schema, isStoredInMemoryOnly: true)
        } else {
            try fileManager.createDirectory(at: root, withIntermediateDirectories: true)
            configuration = ModelConfiguration("RoundPersistence", schema: schema,
                url: root.appendingPathComponent("round-persistence.store"))
        }
        let container = try ModelContainer(for: schema, configurations: [configuration])
        let context = ModelContext(container)
        context.autosaveEnabled = false
        modelContainer = container
        modelExecutor = DefaultSerialModelExecutor(modelContext: context)
    }

    func enqueue(userId: Int, write: PendingHoleWrite) throws {
        try prepare(userId: userId)
        let existing = try writeRecords(userId: userId, roundId: write.roundId, holeNumber: write.holeNumber)
        guard !existing.contains(where: { $0.revision > write.revision }) else { return }
        existing.filter { $0.revision <= write.revision }.forEach(modelContext.delete)
        modelContext.insert(PendingHoleWriteRecord(userId: userId, write: write))
        try modelContext.save()
    }

    func pending(userId: Int, roundId: Int? = nil) throws -> [PendingHoleWrite] {
        try prepare(userId: userId)
        return try records(userId: userId)
            .filter { roundId == nil || $0.roundId == roundId }
            .map(\.value)
            .sorted { lhs, rhs in lhs.roundId == rhs.roundId ? lhs.revision < rhs.revision : lhs.roundId < rhs.roundId }
    }

    func acknowledge(userId: Int, id: UUID) throws {
        try prepare(userId: userId)
        try records(userId: userId).filter { $0.id == id }.forEach(modelContext.delete)
        try modelContext.save()
    }

    func setState(userId: Int, id: UUID, state: SyncRecordState) throws {
        try prepare(userId: userId)
        guard let record = try records(userId: userId).first(where: { $0.id == id }) else { return }
        record.stateRawValue = state.rawValue
        try modelContext.save()
    }

    func saveConflict(userId: Int, write: PendingHoleWrite, serverHole: Hole?) throws {
        try prepare(userId: userId)
        try conflictRecords(userId: userId, roundId: write.roundId)
            .filter { $0.holeNumber == write.holeNumber }.forEach(modelContext.delete)
        if let record = try records(userId: userId).first(where: { $0.id == write.id }) {
            record.stateRawValue = SyncRecordState.conflict.rawValue
        }
        let serverPayload = try serverHole.map { try JSONEncoder.birdieBuddy.encode($0) }
        modelContext.insert(RoundConflictRecord(userId: userId, write: write, serverHolePayload: serverPayload))
        try modelContext.save()
    }

    func conflict(userId: Int, roundId: Int) throws -> PersistedRoundConflict? {
        try prepare(userId: userId)
        return try conflictRecords(userId: userId, roundId: roundId)
            .sorted { $0.discoveredAt < $1.discoveredAt }.first?.value
    }

    func useServerValue(userId: Int, conflictId: UUID) throws -> Hole? {
        try prepare(userId: userId)
        guard let conflict = try conflictRecords(userId: userId).first(where: { $0.id == conflictId }) else { return nil }
        let serverHole = conflict.serverHolePayload.flatMap { try? JSONDecoder.birdieBuddy.decode(Hole.self, from: $0) }
        try records(userId: userId).filter { $0.id == conflict.pendingWriteId }.forEach(modelContext.delete)
        modelContext.delete(conflict)
        try modelContext.save()
        return serverHole
    }

    func keepLocalValue(userId: Int, conflictId: UUID, revision: Int) throws -> PendingHoleWrite? {
        try prepare(userId: userId)
        guard let conflict = try conflictRecords(userId: userId).first(where: { $0.id == conflictId }) else { return nil }
        let local = try JSONDecoder.birdieBuddy.decode(HoleUpsertRequest.self, from: conflict.localPayload)
        let forced = HoleUpsertRequest(par: local.par, score: local.score, putts: local.putts, gir: local.gir,
            fairwayHit: local.fairwayHit, penalty: local.penalty, checkExpected: false, expectedHole: nil)
        let pendingRecord = try records(userId: userId).first { $0.id == conflict.pendingWriteId }
        let nextRevision = max(revision, (pendingRecord?.revision ?? 0) + 1)
        let write = PendingHoleWrite(id: UUID(), roundId: conflict.roundId, holeNumber: conflict.holeNumber,
            revision: nextRevision, expectedUpdatedAt: nil, payload: try JSONEncoder.birdieBuddy.encode(forced))
        if let pendingRecord { modelContext.delete(pendingRecord) }
        modelContext.delete(conflict)
        modelContext.insert(PendingHoleWriteRecord(userId: userId, write: write))
        try modelContext.save()
        return write
    }

    func status(userId: Int, roundId: Int) throws -> RoundSyncStatus {
        try prepare(userId: userId)
        if try !conflictRecords(userId: userId, roundId: roundId).isEmpty { return .reviewRequired }
        let states = try records(userId: userId).filter { $0.roundId == roundId }
            .compactMap { SyncRecordState(rawValue: $0.stateRawValue) }
        if states.contains(.requiresAttention) { return .attentionRequired }
        if states.contains(.syncing) { return .syncing }
        if !states.isEmpty { return .savedOnDevice }
        return .synced
    }

    func statuses(userId: Int) throws -> [Int: RoundSyncStatus] {
        try prepare(userId: userId)
        let roundIds = Set(try records(userId: userId).map(\.roundId) + conflictRecords(userId: userId).map(\.roundId))
        return try Dictionary(uniqueKeysWithValues: roundIds.map { ($0, try status(userId: userId, roundId: $0)) })
    }

    func deleteUserData(userId: Int) throws {
        try prepare(userId: userId)
        try records(userId: userId).forEach(modelContext.delete)
        try conflictRecords(userId: userId).forEach(modelContext.delete)
        try modelContext.save()
    }

    private func prepare(userId: Int) throws {
        guard !migratedUsers.contains(userId) else { return }
        let legacyURL = legacyDirectoryURL.appendingPathComponent("round-outbox-\(userId).json")
        let archiveURL = legacyDirectoryURL.appendingPathComponent("round-outbox-\(userId).json.migrated-v1")
        guard fileManager.fileExists(atPath: legacyURL.path), !fileManager.fileExists(atPath: archiveURL.path) else {
            migratedUsers.insert(userId)
            return
        }
        let writes = try JSONDecoder.birdieBuddy.decode([PendingHoleWrite].self, from: Data(contentsOf: legacyURL))
        for write in writes {
            let older = try writeRecords(userId: userId, roundId: write.roundId, holeNumber: write.holeNumber)
            if older.allSatisfy({ $0.revision <= write.revision }) {
                older.forEach(modelContext.delete)
                modelContext.insert(PendingHoleWriteRecord(userId: userId, write: write))
            }
        }
        try modelContext.save()
        try fileManager.moveItem(at: legacyURL, to: archiveURL)
        migratedUsers.insert(userId)
    }

    private func records(userId: Int) throws -> [PendingHoleWriteRecord] {
        try modelContext.fetch(FetchDescriptor<PendingHoleWriteRecord>(predicate: #Predicate { $0.userId == userId }))
    }

    private func writeRecords(userId: Int, roundId: Int, holeNumber: Int) throws -> [PendingHoleWriteRecord] {
        try modelContext.fetch(FetchDescriptor<PendingHoleWriteRecord>(predicate: #Predicate {
            $0.userId == userId && $0.roundId == roundId && $0.holeNumber == holeNumber
        }))
    }

    private func conflictRecords(userId: Int, roundId: Int? = nil) throws -> [RoundConflictRecord] {
        try modelContext.fetch(FetchDescriptor<RoundConflictRecord>(predicate: #Predicate { $0.userId == userId }))
            .filter { roundId == nil || $0.roundId == roundId }
    }
}

struct SyncReport: Sendable {
    var savedHoles: [Hole] = []
    var conflictRoundIds: Set<Int> = []
}

actor SyncCoordinator {
    private let persistence: RoundPersistenceStore
    private let api: APIClient
    private var activeUsers: Set<Int> = []

    init(persistence: RoundPersistenceStore, api: APIClient) {
        self.persistence = persistence
        self.api = api
    }

    func synchronize(userId: Int, trigger: SyncTrigger, roundId: Int? = nil) async -> SyncReport {
        guard activeUsers.insert(userId).inserted else { return SyncReport() }
        defer { activeUsers.remove(userId) }
        var report = SyncReport()
        guard let writes = try? await persistence.pending(userId: userId, roundId: roundId) else { return report }
        var blockedRounds = Set<Int>()

        for write in writes where write.state != .requiresAttention && write.state != .conflict && !blockedRounds.contains(write.roundId) {
            do {
                try await persistence.setState(userId: userId, id: write.id, state: .syncing)
                let request = try JSONDecoder.birdieBuddy.decode(HoleUpsertRequest.self, from: write.payload)
                let saved = try await api.saveHole(roundId: write.roundId, holeNumber: write.holeNumber, request: request)
                try await persistence.acknowledge(userId: userId, id: write.id)
                report.savedHoles.append(saved)
            } catch let problem as ApiProblem where problem.status == 409 {
                let latest = try? await api.round(id: write.roundId)
                try? await persistence.saveConflict(userId: userId, write: write,
                    serverHole: latest?.holes.first { $0.holeNumber == write.holeNumber })
                blockedRounds.insert(write.roundId)
                report.conflictRoundIds.insert(write.roundId)
            } catch let problem as ApiProblem where problem.status == 401 || problem.status == 403 {
                try? await persistence.setState(userId: userId, id: write.id, state: .queued)
                break
            } catch let problem as ApiProblem where problem.status == 408 || problem.status == 429 || (problem.status ?? 0) >= 500 {
                try? await persistence.setState(userId: userId, id: write.id, state: .queued)
            } catch is URLError {
                try? await persistence.setState(userId: userId, id: write.id, state: .queued)
                break
            } catch let problem as ApiProblem where (400..<500).contains(problem.status ?? 0) {
                try? await persistence.setState(userId: userId, id: write.id, state: .requiresAttention)
            } catch {
                try? await persistence.setState(userId: userId, id: write.id, state: .queued)
            }
        }
        return report
    }
}
