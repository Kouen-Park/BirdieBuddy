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
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
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
    /// Set alongside completion; presenting it pushes the round summary.
    @State private var completedRound: RoundSummary?

    init(draft: RoundDraft) {
        _snapshot = State(initialValue: MutableRoundSnapshot(draft: draft))
        _serverHoles = State(initialValue: Dictionary(uniqueKeysWithValues: draft.holes.map { ($0.holeNumber, $0) }))
        _holeIndex = State(initialValue: Self.resumeHoleIndex(for: draft))
    }

    static func resumeHoleIndex(for draft: RoundDraft) -> Int {
        let saved = Set(draft.holes.map(\.holeNumber))
        let firstUnsaved = (1...max(draft.expectedHoles, 1)).first { !saved.contains($0) }
        return max(0, min((firstUnsaved ?? draft.currentHole) - 1, draft.expectedHoles - 1))
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
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                HStack(alignment: .bottom) {
                    VStack(alignment: .leading, spacing: 5) {
                        Text(draft.courseName.uppercased())
                            .font(BirdieTheme.mono(10))
                            .tracking(1.5)
                            .foregroundStyle(BirdieTheme.fairway)
                        Text(draft.tee)
                            .font(BirdieTheme.display(29))
                            .foregroundStyle(BirdieTheme.ink)
                    }
                    Spacer(minLength: 8)
                    VStack(alignment: .trailing, spacing: 3) {
                        Text("\(draft.holes.count)")
                            .font(BirdieTheme.mono(27))
                            .foregroundStyle(BirdieTheme.ink)
                        Text("\(draft.expectedHoles) holes")
                            .font(BirdieTheme.mono(10))
                            .foregroundStyle(BirdieTheme.muted)
                    }
                }
                if syncStatus != .synced {
                    Label(LocalizedStringKey(syncStatus.label), systemImage: syncStatus == .reviewRequired ? "exclamationmark.triangle" : "icloud.and.arrow.up")
                        .font(BirdieTheme.mono(11))
                        .foregroundStyle(syncStatus == .attentionRequired || syncStatus == .reviewRequired ? BirdieTheme.danger : BirdieTheme.fairwayDark)
                        .padding(.horizontal, 11)
                        .padding(.vertical, 7)
                        .background(BirdieTheme.paper, in: Capsule())
                        .overlay(Capsule().stroke(BirdieTheme.line))
                }
                if !status.isEmpty {
                    Text(LocalizedStringKey(status))
                        .font(BirdieTheme.body(13))
                        .foregroundStyle(BirdieTheme.muted)
                }

                VStack(spacing: 0) {
                    HStack(spacing: 6) {
                        Text("Hole")
                            .frame(maxWidth: .infinity, alignment: .leading)
                        Text("\(holeNumber)")
                            .font(BirdieTheme.display(53))
                            .foregroundStyle(BirdieTheme.paper)
                            .minimumScaleFactor(0.7)
                            .lineLimit(1)
                            .frame(width: 96, height: 96)
                            .background(BirdieTheme.night, in: Circle())
                            .overlay(Circle().stroke(BirdieTheme.sun.opacity(0.7), lineWidth: 2))
                            .accessibilityHidden(true)
                        Text("Par \(expectedPar)")
                            .frame(maxWidth: .infinity, alignment: .trailing)
                    }
                    .font(BirdieTheme.mono(12))
                    .foregroundStyle(BirdieTheme.muted)
                    .accessibilityElement(children: .ignore)
                    .accessibilityLabel("Hole \(holeNumber) of \(draft.expectedHoles)")
                    .accessibilityValue("Par \(expectedPar)")
                    .padding(.bottom, 20)

                    Divider()
                    BirdieCounter(label: "Score", value: $score, range: 1...20)
                    Divider()
                    BirdieCounter(label: "Putts", value: $putts, range: 0...10)
                    Divider()
                    Toggle("Green in regulation", isOn: $gir)
                        .font(BirdieTheme.body(15, weight: .semibold))
                        .tint(BirdieTheme.fairway)
                        .padding(.vertical, 17)
                    Divider()
                    if isPar3 {
                        Text("Fairway does not apply on a par 3")
                            .font(BirdieTheme.body(13))
                            .foregroundStyle(BirdieTheme.muted)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(.vertical, 18)
                    } else {
                        Toggle("Fairway hit", isOn: $fairway)
                            .font(BirdieTheme.body(15, weight: .semibold))
                            .tint(BirdieTheme.fairway)
                            .padding(.vertical, 17)
                    }
                    Divider()
                    BirdieCounter(label: "Penalty strokes", value: $penalty, range: 0...20, compact: true)
                    if !strokeBreakdownIsValid {
                        Label("Putts and penalties cannot exceed the score.", systemImage: "exclamationmark.triangle")
                            .font(BirdieTheme.body(13))
                            .foregroundStyle(BirdieTheme.danger)
                            .padding(.top, 10)
                    }
                }
                .padding(20)
                .background(BirdieTheme.paper, in: UnevenRoundedRectangle(
                    topLeadingRadius: 5, bottomLeadingRadius: 5, bottomTrailingRadius: 22, topTrailingRadius: 22))
                .overlay(UnevenRoundedRectangle(
                    topLeadingRadius: 5, bottomLeadingRadius: 5, bottomTrailingRadius: 22, topTrailingRadius: 22)
                    .stroke(BirdieTheme.line, lineWidth: 1))
                .shadow(color: BirdieTheme.night.opacity(0.07), radius: 15, y: 8)
                .accessibilityIdentifier("liveYardageCard")

                if let errorMessage {
                    Label(errorMessage, systemImage: "exclamationmark.triangle")
                        .font(BirdieTheme.body(13))
                        .foregroundStyle(BirdieTheme.danger)
                        .birdieCard()
                }
            }
            .padding(.horizontal, 16)
            .padding(.top, 16)
            .padding(.bottom, 22)
        }
        .background(BirdieTheme.canvas)
        .safeAreaInset(edge: .bottom) {
            footer
        }
        // The screen stops being a live round the moment the server accepts
        // completion, so it should stop calling itself one.
        .navigationTitle(isRoundComplete ? "Saved round" : "Live round")
        .navigationBarTitleDisplayMode(.inline)
        .navigationDestination(item: $completedRound) { RoundDetailView(round: $0) }
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

    private var footer: some View {
        Group {
            if dynamicTypeSize.isAccessibilitySize {
                VStack(spacing: 8) {
                    advanceHoleAction
                    HStack(spacing: 8) { previousHoleButton; saveHoleAction }
                }
            } else {
                HStack(spacing: 8) {
                    previousHoleButton
                    saveHoleAction
                    advanceHoleAction
                }
            }
        }
        .buttonStyle(.bordered)
        .controlSize(.large)
        .padding(.horizontal, 12)
        .padding(.vertical, 9)
        .background(BirdieTheme.canvas)
    }

    private var saveHoleAction: some View {
        Button(isSaving ? "Saving…" : "Save hole") { Task { await saveHole() } }
            .disabled(isSaving || conflict != nil || !strokeBreakdownIsValid || isRoundComplete)
            .accessibilityLabel("Save hole \(holeNumber)")
            .accessibilityHint("Saves on this device first, then syncs when online")
            .frame(maxWidth: .infinity)
            .tint(BirdieTheme.fairwayDark)
    }

    private var advanceHoleAction: some View {
        advanceButton
            .buttonStyle(.borderedProminent)
            .tint(BirdieTheme.fairwayDark)
            .frame(maxWidth: .infinity)
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

    /// Built from the round the completion call just returned, so the summary opens
    /// without a second request — worth avoiding while the API is slow. The totals
    /// use the server's own definitions (sum of scores, and sum of score minus
    /// par), and RoundDetailView reloads the authoritative detail itself.
    private func summary(for finished: RoundDraft) -> RoundSummary {
        RoundSummary(
            id: finished.id,
            courseId: finished.courseId,
            courseName: finished.courseName,
            date: finished.date,
            tee: finished.tee,
            totalScore: finished.holes.reduce(0) { $0 + $1.score },
            scoreToPar: finished.holes.reduce(0) { $0 + $1.score - $1.par },
            status: finished.status,
            holesPlayed: finished.holes.count,
            expectedHoles: finished.expectedHoles
        )
    }

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
            try await appState.roundPersistence.cacheDraft(userId: userId, draft: draft)
            await appState.refreshSyncStatuses()
        } catch {
            errorMessage = "Could not save this input on the device."
            status = "Local save failed"
            return .failed
        }
        let report = await appState.syncPending(trigger: .manualSave, roundId: draft.id)
        report.savedHoles.forEach { applyServerHole($0) }
        try? await appState.roundPersistence.cacheDraft(userId: userId, draft: draft)
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
            let finished = try await appState.api.complete(roundId: draft.id)
            // The server now refuses live hole edits on this round. Without this
            // flag a second press re-queued hole 18 into a completed round, the
            // server answered 400 "Only a draft round can be edited live", and that
            // write stayed stuck as "action required" for good.
            isRoundComplete = true
            status = "Round complete"
            try? await appState.roundPersistence.removeCachedDraft(userId: userId, roundId: draft.id)
            completedRound = summary(for: finished)
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
        if let userId { try? await appState.roundPersistence.cacheDraft(userId: userId, draft: draft) }
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
            try? await appState.roundPersistence.cacheDraft(userId: userId, draft: draft)
            self.conflict = nil
            let report = await appState.syncPending(trigger: .conflictResolution, roundId: draft.id)
            report.savedHoles.forEach { applyServerHole($0) }
            try? await appState.roundPersistence.cacheDraft(userId: userId, draft: draft)
            if holeNumber == conflict.holeNumber { loadCurrentHole() }
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
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    let conflict: ConflictState
    let onUseServer: () -> Void
    let onKeepLocal: () -> Void

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    Text("Resolve conflict")
                        .font(BirdieTheme.display(29))
                        .foregroundStyle(BirdieTheme.ink)
                    Text("Hole \(conflict.holeNumber) changed on another device")
                        .font(BirdieTheme.body(16, weight: .semibold))
                    Text("Choose which value to keep. Your local input will not be discarded until you choose.")
                        .font(BirdieTheme.body(14))
                        .foregroundStyle(BirdieTheme.muted)
                    VStack(spacing: 0) {
                        comparisonRow("Par", local: "\(conflict.local.par ?? 0)", server: conflict.server.map { "\($0.par)" } ?? "—")
                        comparisonRow("Score", local: "\(conflict.local.score)", server: conflict.server.map { "\($0.score)" } ?? "—")
                        comparisonRow("Putts", local: "\(conflict.local.putts)", server: conflict.server.map { "\($0.putts)" } ?? "—")
                        comparisonRow("GIR", local: yesNo(conflict.local.gir), server: conflict.server.map { yesNo($0.gir) } ?? "—")
                        comparisonRow("Fairway", local: conflict.local.fairwayHit.map(yesNo) ?? "—",
                                      server: conflict.server?.fairwayHit.map(yesNo) ?? "—")
                        comparisonRow("Penalties", local: "\(conflict.local.penalty)",
                                      server: conflict.server.map { "\($0.penalty)" } ?? "—")
                    }
                    .birdieCard()
                    VStack(spacing: 10) {
                        Button("Keep my value", action: onKeepLocal)
                            .buttonStyle(.borderedProminent)
                            .tint(BirdieTheme.fairwayDark)
                            .accessibilityLabel("Keep this device's value for hole \(conflict.holeNumber)")
                        Button("Use server value", action: onUseServer)
                            .buttonStyle(.bordered)
                            .tint(BirdieTheme.fairwayDark)
                            .accessibilityLabel("Use the server value for hole \(conflict.holeNumber)")
                    }
                    .controlSize(.large)
                    .frame(maxWidth: .infinity)
                }
                .padding(20)
            }
            .background(BirdieTheme.canvas)
            .navigationTitle("Resolve conflict")
            .navigationBarTitleDisplayMode(.inline)
        }
    }

    private func yesNo(_ value: Bool) -> String {
        value ? String(localized: "Yes") : String(localized: "No")
    }

    private func comparisonRow(_ label: LocalizedStringKey, local: String, server: String) -> some View {
        Group {
            if dynamicTypeSize.isAccessibilitySize {
                VStack(alignment: .leading, spacing: 4) {
                    Text(label).font(BirdieTheme.body(14, weight: .bold))
                    Text("\(String(localized: "On this device")): \(local)")
                    Text("\(String(localized: "On server")): \(server)")
                }
            } else {
                HStack(spacing: 8) {
                    Text(label).frame(maxWidth: .infinity, alignment: .leading)
                    Text(local).frame(width: 90, alignment: .leading)
                    Text(server).frame(width: 90, alignment: .leading)
                }
                .font(BirdieTheme.mono(11))
            }
        }
        .foregroundStyle(BirdieTheme.ink)
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.vertical, 9)
        .overlay(alignment: .bottom) { BirdieTheme.line.frame(height: 1) }
    }
}
