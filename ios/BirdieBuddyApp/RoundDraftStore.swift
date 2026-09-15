import Foundation

struct PendingHoleWrite: Codable, Equatable, Identifiable {
    let id: UUID
    let roundId: Int
    let holeNumber: Int
    let revision: Int
    let expectedUpdatedAt: Date?
    let payload: Data
}

actor RoundDraftStore {
    private let url: URL
    private var writes: [PendingHoleWrite] = []

    init(userId: Int, fileManager: FileManager = .default, directoryURL: URL? = nil) {
        let root = directoryURL ?? fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        url = root.appendingPathComponent("round-outbox-\(userId).json")
    }

    func load() throws -> [PendingHoleWrite] {
        guard FileManager.default.fileExists(atPath: url.path) else { return writes }
        writes = try JSONDecoder.birdieBuddy.decode([PendingHoleWrite].self, from: Data(contentsOf: url))
        return writes
    }

    func enqueue(_ write: PendingHoleWrite) throws {
        writes.removeAll { $0.roundId == write.roundId && $0.holeNumber == write.holeNumber && $0.revision <= write.revision }
        writes.append(write)
        try persist()
    }

    func acknowledge(_ id: UUID) throws { writes.removeAll { $0.id == id }; try persist() }
    func pending(for roundId: Int) -> [PendingHoleWrite] { writes.filter { $0.roundId == roundId }.sorted { $0.revision < $1.revision } }

    private func persist() throws {
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        try JSONEncoder.birdieBuddy.encode(writes).write(to: url, options: .atomic)
    }
}
