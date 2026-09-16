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
    private let fileManager: FileManager
    private let url: URL
    private var writes: [PendingHoleWrite] = []
    private var isLoaded = false

    init(userId: Int, fileManager: FileManager = .default, directoryURL: URL? = nil) {
        self.fileManager = fileManager
        let root = directoryURL ?? fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        url = root.appendingPathComponent("round-outbox-\(userId).json")
    }

    func load() throws -> [PendingHoleWrite] {
        try ensureLoaded()
        return writes
    }

    func enqueue(_ write: PendingHoleWrite) throws {
        try ensureLoaded()
        writes.removeAll { $0.roundId == write.roundId && $0.holeNumber == write.holeNumber && $0.revision <= write.revision }
        writes.append(write)
        try persist()
    }

    func acknowledge(_ id: UUID) throws {
        try ensureLoaded()
        writes.removeAll { $0.id == id }
        try persist()
    }

    func pending(for roundId: Int) throws -> [PendingHoleWrite] {
        try ensureLoaded()
        return writes.filter { $0.roundId == roundId }.sorted { $0.revision < $1.revision }
    }

    private func ensureLoaded() throws {
        guard !isLoaded else { return }
        guard fileManager.fileExists(atPath: url.path) else {
            writes = []
            isLoaded = true
            return
        }
        writes = try JSONDecoder.birdieBuddy.decode([PendingHoleWrite].self, from: Data(contentsOf: url))
        isLoaded = true
    }

    private func persist() throws {
        try fileManager.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        try JSONEncoder.birdieBuddy.encode(writes).write(to: url, options: .atomic)
    }
}
