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
    private var userId: Int? { appState.user?.id }
    private var syncStatus: RoundSyncStatus { appState.roundSyncStatuses[draft.id] ?? .synced }

    var body: some View {
        Form {
            Section {
                Text("Hole \(holeNumber) of \(draft.expectedHoles)").font(.title2.bold())
                    .accessibilityAddTraits(.isHeader)
                if syncStatus != .synced {
                    Label(syncStatus.label, systemImage: syncStatus == .reviewRequired ? "exclamationmark.triangle" : "icloud.and.arrow.up")
                        .foregroundStyle(syncStatus == .attentionRequired || syncStatus == .reviewRequired ? .orange : .secondary)
                        .accessibilityLabel("Sync state: \(syncStatus.label)")
                }
                Text(LocalizedStringKey(status)).foregroundStyle(.secondary)
                    .accessibilityLabel(status.isEmpty ? "No save status yet" : "Save status: \(status)")
            }
            Section("Score") {
                Stepper("Score: \(score)", value: $score, in: 1...20)
                    .accessibilityLabel("Score")
                    .accessibilityValue("\(score) strokes")
                Stepper("Putts: \(putts)", value: $putts, in: 0...10)
                    .accessibilityLabel("Putts")
                    .accessibilityValue("\(putts)")
                Toggle("Green in regulation", isOn: $gir)
                Toggle("Fairway hit", isOn: $fairway)
                Stepper("Penalty: \(penalty)", value: $penalty, in: 0...20)
                    .accessibilityLabel("Penalty strokes")
                    .accessibilityValue("\(penalty)")
            }
            if let errorMessage { Text(errorMessage).foregroundStyle(.red) }
            Section {
                Button(isSaving ? "Saving…" : "Save hole") { Task { await saveHole() } }
                    .disabled(isSaving || conflict != nil)
                    .accessibilityLabel("Save hole \(holeNumber)")
                    .accessibilityHint("Saves on this device first, then syncs when online")
                HStack {
                    Button("Previous") { move(-1) }.disabled(holeIndex == 0 || isSaving || conflict != nil)
                        .accessibilityLabel("Previous hole")
                    Spacer()
                    Button(holeNumber == draft.expectedHoles ? "Finish round" : "Next hole") {
                        Task { await advance() }
                    }
                    .disabled(isSaving || conflict != nil)
                    .accessibilityLabel(holeNumber == draft.expectedHoles ? "Finish round" : "Save and go to hole \(holeNumber + 1)")
                }
            }
        }
        .navigationTitle("Live round")
        .onAppear { loadCurrentHole() }
        .task { await restoreConflictAndSync() }
        .onChange(of: holeIndex) { _, _ in loadCurrentHole() }
        .sheet(item: $conflict) { conflict in
            ConflictReviewView(conflict: conflict, onUseServer: {
                isSaving = true
                Task { await useServerValue(for: conflict) }
            }, onKeepLocal: {
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
        guard let userId else {
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
                payload: try JSONEncoder.birdieBuddy.encode(request),
                serverSnapshot: try expectedServerHole.map { try JSONEncoder.birdieBuddy.encode($0) }
            )
            try await appState.roundPersistence.enqueue(userId: userId, write: write)
            applyLocalRequest(request, holeNumber: holeNumber)
            await appState.refreshSyncStatuses()
        } catch {
            errorMessage = "Could not save this input on the device."
            status = "Local save failed"
            return .failed
        }
        let report = await appState.syncPending(trigger: .manualSave, roundId: draft.id)
        report.savedHoles.forEach { applyServerHole($0) }
        await loadPersistedConflict()
        switch syncStatus {
        case .synced: status = "Saved to server"; return .serverSaved
        case .reviewRequired: status = "Review required"; return .conflict
        case .savedOnDevice, .syncing: status = "Saved on device — will sync when online"; return .queuedOffline
        case .attentionRequired: status = "Saved on device — action required"; return .failed
        }
    }

    private func advance() async {
        let outcome = await saveHole()
        if holeNumber < draft.expectedHoles {
            guard outcome.permitsHoleNavigation else { return }
            move(1)
            return
        }

        guard outcome.permitsCompletion, conflict == nil, let userId else { return }
        do {
            guard try await appState.roundPersistence.pending(userId: userId, roundId: draft.id).isEmpty else {
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

    private func restoreConflictAndSync() async {
        let report = await appState.syncPending(trigger: .liveRound, roundId: draft.id)
        report.savedHoles.forEach { applyServerHole($0) }
        await loadPersistedConflict()
    }

    private func loadPersistedConflict() async {
        guard let userId,
              let persisted = try? await appState.roundPersistence.conflict(userId: userId, roundId: draft.id) else {
            conflict = nil
            return
        }
        do {
            let local = try JSONDecoder.birdieBuddy.decode(HoleUpsertRequest.self, from: persisted.localPayload)
            let server = persisted.serverHolePayload.flatMap { try? JSONDecoder.birdieBuddy.decode(Hole.self, from: $0) }
            conflict = ConflictState(id: persisted.id, holeNumber: persisted.holeNumber, local: local, server: server)
            status = "Sync paused — review hole \(persisted.holeNumber)"
        } catch {
            errorMessage = "The saved conflict could not be opened."
        }
    }

    private func useServerValue(for conflict: ConflictState) async {
        defer { isSaving = false }
        guard let userId else { return }
        do {
            if let server = try await appState.roundPersistence.useServerValue(userId: userId, conflictId: conflict.id) {
                applyServerHole(server)
                loadCurrentHole()
            }
            self.conflict = nil
            status = "Using server value"
            await appState.refreshSyncStatuses()
            await restoreConflictAndSync()
        } catch {
            errorMessage = "Could not update the saved changes on this device."
        }
    }

    private func keepLocalValue(for conflict: ConflictState) async {
        defer { isSaving = false }
        guard let userId else {
            errorMessage = "Sign in again before saving this round."
            return
        }
        do {
            let revision = Int(Date.timeIntervalSinceReferenceDate * 1000)
            guard try await appState.roundPersistence.keepLocalValue(userId: userId, conflictId: conflict.id, revision: revision) != nil else { return }
            applyLocalRequest(conflict.local, holeNumber: conflict.holeNumber)
            self.conflict = nil
            let report = await appState.syncPending(trigger: .conflictResolution, roundId: draft.id)
            report.savedHoles.forEach { applyServerHole($0) }
            status = appState.roundSyncStatuses[draft.id] == .synced ? "Kept this device's value" : "Saved on device — will sync when online"
            await loadPersistedConflict()
        } catch {
            errorMessage = "Could not save this input on the device."
            status = "Local save failed"
        }
    }
}

struct ConflictState: Identifiable {
    let id: UUID
    let holeNumber: Int
    let local: HoleUpsertRequest
    let server: Hole?
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
                    .accessibilityLabel("Use the server value for hole \(conflict.holeNumber)")
                Button("Keep my value", action: onKeepLocal)
                    .accessibilityLabel("Keep this device's value for hole \(conflict.holeNumber)")
            }
            .navigationTitle("Resolve conflict")
        }
    }
}
