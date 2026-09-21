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
    /// Par per hole number, from the round's tee. The draft carries no hole rows
    /// until they are saved, so without this the app assumed par 4 everywhere —
    /// and then sent a fairway value the server rejects on a par 3.
    @State private var coursePars: [Int: Int] = [:]
    /// Set once the server accepts completion. The round stops accepting hole
    /// writes at that moment, so the controls must stop offering them.
    @State private var isRoundComplete = false

    init(draft: RoundDraft) {
        _snapshot = State(initialValue: MutableRoundSnapshot(draft: draft))
        _serverHoles = State(initialValue: Dictionary(uniqueKeysWithValues: draft.holes.map { ($0.holeNumber, $0) }))
    }

    /// Used until the tee's pars arrive, and as the last resort if they never do.
    private static let assumedPar = 4

    private var holeNumber: Int { holeIndex + 1 }
    private var draft: RoundDraft { snapshot.draft }
    private var currentHole: Hole? { snapshot.hole(number: holeNumber) }
    private var expectedServerHole: Hole? { serverHoles[holeNumber] }
    private var expectedPar: Int { currentHole?.par ?? coursePars[holeNumber] ?? Self.assumedPar }
    private var isPar3: Bool { expectedPar == 3 }
    private var userId: Int? { appState.user?.id }
    private var syncStatus: RoundSyncStatus { appState.roundSyncStatuses[draft.id] ?? .synced }

    /// The server refuses putts + penalties above the score, so block the request
    /// here rather than letting it fail after the golfer has moved on.
    private var strokeBreakdownIsValid: Bool { putts + penalty <= score }

    var body: some View {
        Form {
            Section {
                Text("Hole \(holeNumber) of \(draft.expectedHoles)").font(.title2.bold())
                    .accessibilityAddTraits(.isHeader)
                if syncStatus != .synced {
                    // LocalizedStringKey, not the raw String: a String argument skips
                    // the catalog, which is why this line stayed English on a Korean
                    // device while the line below it was translated.
                    Label(LocalizedStringKey(syncStatus.label), systemImage: syncStatus == .reviewRequired ? "exclamationmark.triangle" : "icloud.and.arrow.up")
                        .foregroundStyle(syncStatus == .attentionRequired || syncStatus == .reviewRequired ? .orange : .secondary)
                        .accessibilityLabel("Sync state: \(syncStatus.label)")
                }
                Text(LocalizedStringKey(status)).foregroundStyle(.secondary)
                    .accessibilityLabel(status.isEmpty ? "No save status yet" : "Save status: \(status)")
            }
            Section("Score") {
                LabeledContent("Par", value: "\(expectedPar)")
                Stepper("Score: \(score)", value: $score, in: 1...20)
                    .accessibilityLabel("Score")
                    .accessibilityValue("\(score) strokes")
                Stepper("Putts: \(putts)", value: $putts, in: 0...10)
                    .accessibilityLabel("Putts")
                    .accessibilityValue("\(putts)")
                Toggle("Green in regulation", isOn: $gir)
                if isPar3 {
                    // The server rejects a fairway value on a par 3, and a par 3 has
                    // no fairway to hit, so the control is not offered.
                    Text("Fairway does not apply on a par 3")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                } else {
                    Toggle("Fairway hit", isOn: $fairway)
                }
                Stepper("Penalty: \(penalty)", value: $penalty, in: 0...20)
                    .accessibilityLabel("Penalty strokes")
                    .accessibilityValue("\(penalty)")
                if !strokeBreakdownIsValid {
                    Label("Putts and penalties cannot exceed the score.", systemImage: "exclamationmark.triangle")
                        .font(.footnote)
                        .foregroundStyle(.orange)
                }
            }
            if let errorMessage { Text(errorMessage).foregroundStyle(.red) }
            Section {
                Button(isSaving ? "Saving…" : "Save hole") { Task { await saveHole() } }
                    .disabled(isSaving || conflict != nil || !strokeBreakdownIsValid || isRoundComplete)
                    .accessibilityLabel("Save hole \(holeNumber)")
                    .accessibilityHint("Saves on this device first, then syncs when online")
                // One button per row. Two buttons in a single Form row make the row
                // route its tap to the first ENABLED one, so on hole 2 onwards a tap
                // meant for Next was consumed by Previous and the round jumped back
                // to hole 1. On hole 1 Previous is disabled, which is the only
                // reason advancing appeared to work there.
                previousHoleButton
                advanceButton
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

    private var previousHoleButton: some View {
        Button("Previous") { move(-1) }
            .disabled(holeIndex == 0 || isSaving || conflict != nil || isRoundComplete)
            .accessibilityLabel("Previous hole")
    }

    private var advanceButton: some View {
        Button(holeNumber == draft.expectedHoles ? "Finish round" : "Next hole") {
            Task { await advance() }
        }
        .disabled(isSaving || conflict != nil || isRoundComplete)
        .accessibilityLabel(holeNumber == draft.expectedHoles ? "Finish round" : "Save and go to hole \(holeNumber + 1)")
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
        // fairwayHit must be absent on a par 3: the server rejects a value there.
        let request = HoleUpsertRequest(par: expectedPar, score: score, putts: putts, gir: gir,
            fairwayHit: isPar3 ? nil : fairway, penalty: penalty,
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

        // Judge THIS hole, not the round. Round-level status made one permanently
        // rejected hole block navigation on every other hole of the round.
        let outstanding = (try? await appState.roundPersistence.pending(userId: userId, roundId: draft.id))?
            .filter { $0.holeNumber == holeNumber } ?? []

        if outstanding.isEmpty {
            status = "Saved to server"
            return .serverSaved
        }
        if outstanding.contains(where: { $0.state == .conflict }) || syncStatus == .reviewRequired {
            status = "Review required"
            return .conflict
        }
        if outstanding.contains(where: { $0.state == .requiresAttention }) {
            status = "Saved on device — action required"
            errorMessage = report.attentionReasons[holeNumber]
                ?? "The server rejected this hole. Check the values and save again."
            return .failed
        }
        status = "Saved on device — will sync when online"
        return .queuedOffline
    }

    private func advance() async {
        let outcome = await saveHole()
        if holeNumber < draft.expectedHoles {
            guard outcome.permitsHoleNavigation else { return }
            move(1)
            return
        }

        guard conflict == nil, let userId else { return }
        switch outcome {
        case .conflict, .failed:
            // saveHole already put the reason on screen.
            return
        case .queuedOffline:
            // Finishing needs the server. Previously this fell through a guard that
            // returned silently, so pressing Finish offline did nothing and said
            // nothing — the message below was unreachable without a connection.
            status = "Sync all saved holes before finishing"
            return
        case .serverSaved:
            break
        }
        do {
            guard try await appState.roundPersistence.pending(userId: userId, roundId: draft.id).isEmpty else {
                status = "Sync all saved holes before finishing"
                return
            }
            _ = try await appState.api.complete(roundId: draft.id)
            // The server now refuses live hole edits on this round. Without this
            // flag a second press re-queued hole 18 into a completed round, the
            // server answered 400 "Only a draft round can be edited live", and that
            // write stayed stuck as "action required" for good.
            isRoundComplete = true
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
        await loadCoursePars()
        let report = await appState.syncPending(trigger: .liveRound, roundId: draft.id)
        report.savedHoles.forEach { applyServerHole($0) }
        await loadPersistedConflict()
    }

    /// Reads par per hole from the round's tee. Best effort: without it the app
    /// falls back to par 4, which is only a display and default-score guess.
    private func loadCoursePars() async {
        guard coursePars.isEmpty, let course = try? await appState.api.course(id: draft.courseId) else { return }
        let tee = course.tees.first { $0.id == draft.courseTeeId } ?? course.tees.first
        let holes = tee?.holes.isEmpty == false ? tee!.holes : course.holes
        coursePars = Dictionary(holes.map { ($0.holeNumber, $0.par) }, uniquingKeysWith: { first, _ in first })
        // Correct the default score only, and only while this hole has nothing
        // saved and the score is still the pre-par guess. Re-running
        // loadCurrentHole() here discarded anything entered while the course was
        // loading, and it wrote view state a second time in the same frame.
        if currentHole == nil, score == Self.assumedPar { score = expectedPar }
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
