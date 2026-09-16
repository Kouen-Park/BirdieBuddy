import SwiftUI

struct MutableRoundSnapshot: Equatable {
    private(set) var draft: RoundDraft

    func hole(number: Int) -> Hole? {
        draft.holes.first { $0.holeNumber == number }
    }

    mutating func apply(_ hole: Hole) {
        var holes = draft.holes.filter { $0.holeNumber != hole.holeNumber }
        holes.append(hole)
        holes.sort { $0.holeNumber < $1.holeNumber }
        draft = RoundDraft(
            id: draft.id,
            courseId: draft.courseId,
            courseName: draft.courseName,
            date: draft.date,
            courseTeeId: draft.courseTeeId,
            tee: draft.tee,
            holes: holes,
            status: draft.status,
            currentHole: max(draft.currentHole, hole.holeNumber),
            expectedHoles: draft.expectedHoles,
            updatedAt: draft.updatedAt
        )
    }
}

enum HoleSaveOutcome: Equatable {
    case serverSaved
    case queuedOffline
    case conflict
    case failed

    var permitsHoleNavigation: Bool {
        self == .serverSaved || self == .queuedOffline
    }

    var permitsCompletion: Bool {
        self == .serverSaved
    }
}

struct LiveRoundView: View {
    @EnvironmentObject private var appState: AppState
    @State private var snapshot: MutableRoundSnapshot
    @State private var serverHoles: [Int: Hole]
    @State private var holeIndex = 0
    @State private var score = 4
    @State private var putts = 2
    @State private var gir = false
    @State private var fairway = false
    @State private var penalty = 0
    @State private var status = ""
    @State private var errorMessage: String?
    @State private var isSaving = false
    @StateObject private var connectivity = ConnectivityMonitor()
    @State private var conflict: ConflictState?

    init(draft: RoundDraft) {
        _snapshot = State(initialValue: MutableRoundSnapshot(draft: draft))
        _serverHoles = State(initialValue: Dictionary(uniqueKeysWithValues: draft.holes.map { ($0.holeNumber, $0) }))
    }

    private var holeNumber: Int { holeIndex + 1 }
    private var draft: RoundDraft { snapshot.draft }
    private var currentHole: Hole? { snapshot.hole(number: holeNumber) }
    private var expectedServerHole: Hole? { serverHoles[holeNumber] }
    private var expectedPar: Int { currentHole?.par ?? 4 }
    private var outbox: RoundDraftStore? {
        guard let userId = appState.user?.id else { return nil }
        return appState.roundDraftStore(for: userId)
    }

    var body: some View {
        Form {
            Section {
                Text("Hole \(holeNumber) of \(draft.expectedHoles)").font(.title2.bold())
                if !connectivity.isOnline { Label("Offline — saved on this device", systemImage: "icloud.slash") }
                Text(status).foregroundStyle(.secondary)
            }
            Section("Score") {
                Stepper("Score: \(score)", value: $score, in: 1...20)
                Stepper("Putts: \(putts)", value: $putts, in: 0...10)
                Toggle("Green in regulation", isOn: $gir)
                Toggle("Fairway hit", isOn: $fairway)
                Stepper("Penalty: \(penalty)", value: $penalty, in: 0...20)
            }
            if let errorMessage { Text(errorMessage).foregroundStyle(.red) }
            Section {
                Button(isSaving ? "Saving…" : "Save hole") { Task { await saveHole() } }
                    .disabled(isSaving || conflict != nil)
                HStack {
                    Button("Previous") { move(-1) }.disabled(holeIndex == 0 || isSaving || conflict != nil)
                    Spacer()
                    Button(holeNumber == draft.expectedHoles ? "Finish round" : "Next hole") {
                        Task { await advance() }
                    }
                    .disabled(isSaving || conflict != nil)
                }
            }
        }
        .navigationTitle("Live round")
        .onAppear { loadCurrentHole() }
        .task { await syncOutbox() }
        .onChange(of: holeIndex) { _, _ in loadCurrentHole() }
        .onChange(of: connectivity.isOnline) { _, online in
            if online { Task { await syncOutbox() } }
        }
        .sheet(item: $conflict) { conflict in
            ConflictReviewView(conflict: conflict, onUseServer: {
                self.conflict = nil
                isSaving = true
                if let server = conflict.server {
                    applyServerHole(server)
                    loadCurrentHole()
                }
                Task {
                    defer { isSaving = false }
                    do {
                        try await outbox?.acknowledge(conflict.pending.id)
                        status = "Using server value"
                    } catch {
                        errorMessage = "Could not update the saved changes on this device."
                    }
                }
            }, onKeepLocal: {
                self.conflict = nil
                isSaving = true
                Task { await keepLocalValue(for: conflict) }
            })
            .interactiveDismissDisabled()
        }
    }

    private func loadCurrentHole() {
        let hole = currentHole
        score = hole?.score ?? expectedPar; putts = hole?.putts ?? 2; gir = hole?.gir ?? false
        fairway = hole?.fairwayHit ?? false; penalty = hole?.penalty ?? 0
    }

    private func move(_ delta: Int) { holeIndex = min(max(0, holeIndex + delta), draft.expectedHoles - 1) }

    @discardableResult
    private func saveHole(force: Bool = false) async -> HoleSaveOutcome {
        isSaving = true; errorMessage = nil
        defer { isSaving = false }
        let request = HoleUpsertRequest(par: expectedPar, score: score, putts: putts, gir: gir, fairwayHit: fairway, penalty: penalty,
            checkExpected: !force, expectedHole: force ? nil : expectedServerHole)
        guard let store = outbox else {
            errorMessage = "Sign in again before saving this round."
            status = "Local save failed"
            return .failed
        }
        let write: PendingHoleWrite
        do {
            write = PendingHoleWrite(
                id: UUID(),
                roundId: draft.id,
                holeNumber: holeNumber,
                revision: Int(Date.timeIntervalSinceReferenceDate * 1000),
                expectedUpdatedAt: draft.updatedAt,
                payload: try JSONEncoder.birdieBuddy.encode(request)
            )
            try await store.enqueue(write)
            applyLocalRequest(request, holeNumber: holeNumber)
        } catch {
            errorMessage = "Could not save this input on the device."
            status = "Local save failed"
            return .failed
        }
        do {
            let saved = try await appState.api.saveHole(roundId: draft.id, holeNumber: holeNumber, request: request)
            try await store.acknowledge(write.id)
            applyServerHole(saved)
            status = "Saved to server"
            return .serverSaved
        } catch let problem as ApiProblem where problem.status == 409 {
            do {
                let latest = try await appState.api.round(id: draft.id)
                conflict = ConflictState(holeNumber: holeNumber, local: request, server: latest.holes.first { $0.holeNumber == holeNumber }, pending: write)
                status = "Review required"
                return .conflict
            } catch {
                errorMessage = AppState.message(for: error)
                return .failed
            }
        } catch is URLError {
            status = "Saved on device — will sync when online"
            return .queuedOffline
        } catch {
            errorMessage = AppState.message(for: error)
            status = "Saved on device — sync paused"
            return .failed
        }
    }

    private func syncOutbox() async {
        guard let userId = appState.user?.id else { return }
        let store = appState.roundDraftStore(for: userId)
        do {
            let pending = try await store.pending(for: draft.id)
            for write in pending {
                let request = try JSONDecoder.birdieBuddy.decode(HoleUpsertRequest.self, from: write.payload)
                do {
                    let saved = try await appState.api.saveHole(roundId: write.roundId, holeNumber: write.holeNumber, request: request)
                    try await store.acknowledge(write.id)
                    applyServerHole(saved)
                } catch let problem as ApiProblem where problem.status == 409 {
                    do {
                        let latest = try await appState.api.round(id: draft.id)
                        conflict = ConflictState(
                            holeNumber: write.holeNumber,
                            local: request,
                            server: latest.holes.first { $0.holeNumber == write.holeNumber },
                            pending: write
                        )
                        status = "Sync paused — review hole \(write.holeNumber)"
                    } catch {
                        errorMessage = AppState.message(for: error)
                        status = "Sync paused — changes remain on device"
                    }
                    return
                }
            }
            if !pending.isEmpty { status = "Offline changes synced" }
        } catch { status = "Sync unavailable — changes remain on device" }
    }

    private func advance() async {
        let outcome = await saveHole()
        if holeNumber < draft.expectedHoles {
            guard outcome.permitsHoleNavigation else { return }
            move(1)
            return
        }

        guard outcome.permitsCompletion, conflict == nil, let store = outbox else { return }
        do {
            guard try await store.pending(for: draft.id).isEmpty else {
                status = "Sync all saved holes before finishing"
                return
            }
            _ = try await appState.api.complete(roundId: draft.id)
            status = "Round complete"
        } catch {
            errorMessage = AppState.message(for: error)
        }
    }

    private func applyLocalRequest(_ request: HoleUpsertRequest, holeNumber: Int) {
        let existing = snapshot.hole(number: holeNumber)
        snapshot.apply(Hole(
            id: existing?.id ?? 0,
            holeNumber: holeNumber,
            par: request.par ?? existing?.par ?? 4,
            score: request.score,
            putts: request.putts,
            gir: request.gir,
            fairwayHit: request.fairwayHit,
            penalty: request.penalty
        ))
    }

    private func applyServerHole(_ hole: Hole) {
        snapshot.apply(hole)
        serverHoles[hole.holeNumber] = hole
    }

    private func keepLocalValue(for conflict: ConflictState) async {
        defer { isSaving = false }
        guard let store = outbox else {
            errorMessage = "Sign in again before saving this round."
            return
        }
        let request = HoleUpsertRequest(
            par: conflict.local.par,
            score: conflict.local.score,
            putts: conflict.local.putts,
            gir: conflict.local.gir,
            fairwayHit: conflict.local.fairwayHit,
            penalty: conflict.local.penalty,
            checkExpected: false,
            expectedHole: nil
        )
        do {
            let write = PendingHoleWrite(
                id: UUID(),
                roundId: conflict.pending.roundId,
                holeNumber: conflict.holeNumber,
                revision: max(Int(Date.timeIntervalSinceReferenceDate * 1000), conflict.pending.revision + 1),
                expectedUpdatedAt: conflict.pending.expectedUpdatedAt,
                payload: try JSONEncoder.birdieBuddy.encode(request)
            )
            try await store.enqueue(write)
            applyLocalRequest(request, holeNumber: conflict.holeNumber)
            do {
                let saved = try await appState.api.saveHole(roundId: write.roundId, holeNumber: write.holeNumber, request: request)
                try await store.acknowledge(write.id)
                applyServerHole(saved)
                status = "Kept this device's value"
            } catch is URLError {
                status = "Saved on device — will sync when online"
            } catch {
                errorMessage = AppState.message(for: error)
                status = "Saved on device — sync paused"
            }
        } catch {
            errorMessage = "Could not save this input on the device."
            status = "Local save failed"
        }
    }
}

struct ConflictState: Identifiable {
    let id = UUID()
    let holeNumber: Int
    let local: HoleUpsertRequest
    let server: Hole?
    let pending: PendingHoleWrite
}

struct ConflictReviewView: View {
    let conflict: ConflictState
    let onUseServer: () -> Void
    let onKeepLocal: () -> Void

    var body: some View {
        NavigationStack {
            Form {
                Section("Hole \(conflict.holeNumber) changed on another device") {
                    Text("Choose which value to keep. Your local input will not be discarded until you choose.")
                }
                Section("On this device") {
                    LabeledContent("Score", value: "\(conflict.local.score)")
                    LabeledContent("Putts", value: "\(conflict.local.putts)")
                    LabeledContent("GIR", value: conflict.local.gir ? "Yes" : "No")
                }
                Section("On server") {
                    LabeledContent("Score", value: "\(conflict.server?.score ?? 0)")
                    LabeledContent("Putts", value: "\(conflict.server?.putts ?? 0)")
                    LabeledContent("GIR", value: conflict.server?.gir == true ? "Yes" : "No")
                }
                Button("Use server value", action: onUseServer)
                Button("Keep my value", action: onKeepLocal)
            }
            .navigationTitle("Resolve conflict")
        }
    }
}
