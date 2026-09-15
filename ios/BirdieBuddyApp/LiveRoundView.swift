import SwiftUI

struct LiveRoundView: View {
    @EnvironmentObject private var appState: AppState
    let draft: RoundDraft
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

    private var outbox: RoundDraftStore { RoundDraftStore(userId: appState.user?.id ?? 0) }

    private var holeNumber: Int { holeIndex + 1 }
    private var currentHole: Hole? { draft.holes.first { $0.holeNumber == holeNumber } }
    private var expectedPar: Int { currentHole?.par ?? 4 }

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
                    .disabled(isSaving)
                HStack {
                    Button("Previous") { move(-1) }.disabled(holeIndex == 0)
                    Spacer()
                    Button(holeNumber == draft.expectedHoles ? "Finish round" : "Next hole") {
                        Task { await advance() }
                    }
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
                if let server = conflict.server {
                    score = server.score; putts = server.putts; gir = server.gir
                    fairway = server.fairwayHit ?? false; penalty = server.penalty
                }
                Task { try? await outbox.acknowledge(conflict.pendingID) }
                status = "Using server value"
            }, onKeepLocal: {
                self.conflict = nil
                Task { await saveHole(force: true) }
            })
        }
    }

    private func loadCurrentHole() {
        let hole = currentHole
        score = hole?.score ?? expectedPar; putts = hole?.putts ?? 2; gir = hole?.gir ?? false
        fairway = hole?.fairwayHit ?? false; penalty = hole?.penalty ?? 0
    }

    private func move(_ delta: Int) { holeIndex = min(max(0, holeIndex + delta), draft.expectedHoles - 1) }

    private func saveHole(force: Bool = false) async {
        isSaving = true; errorMessage = nil
        let request = HoleUpsertRequest(par: expectedPar, score: score, putts: putts, gir: gir, fairwayHit: fairway, penalty: penalty,
            checkExpected: !force, expectedHole: force ? nil : currentHole)
        let writeId = UUID()
        let store = outbox
        do {
            let payload = try JSONEncoder.birdieBuddy.encode(request)
            try await store.enqueue(PendingHoleWrite(id: writeId, roundId: draft.id, holeNumber: holeNumber,
                revision: Int(Date.timeIntervalSinceReferenceDate * 1000), expectedUpdatedAt: draft.updatedAt, payload: payload))
        } catch {
            errorMessage = "Could not save this input on the device."
            status = "Local save failed"
            isSaving = false
            return
        }
        do {
            _ = try await appState.api.saveHole(roundId: draft.id, holeNumber: holeNumber, request: request)
            try await store.acknowledge(writeId)
            status = "Saved to server"
        } catch let problem as ApiProblem where problem.status == 409 {
            do {
                let latest = try await appState.api.round(id: draft.id)
                conflict = ConflictState(holeNumber: holeNumber, local: request, server: latest.holes.first { $0.holeNumber == holeNumber }, pendingID: writeId)
                status = "Review required"
            } catch { errorMessage = AppState.message(for: error) }
        } catch is URLError {
            status = "Saved on device — will sync when online"
        } catch { errorMessage = AppState.message(for: error); status = "Not saved" }
        isSaving = false
    }

    private func syncOutbox() async {
        guard let userId = appState.user?.id else { return }
        let store = RoundDraftStore(userId: userId)
        do {
            let pending = try await store.load().filter { $0.roundId == draft.id }
            for write in pending {
                let request = try JSONDecoder.birdieBuddy.decode(HoleUpsertRequest.self, from: write.payload)
                do {
                    _ = try await appState.api.saveHole(roundId: write.roundId, holeNumber: write.holeNumber, request: request)
                    try await store.acknowledge(write.id)
                } catch let problem as ApiProblem where problem.status == 409 {
                    status = "Sync paused — review hole \(write.holeNumber)"
                    return
                }
            }
            if !pending.isEmpty { status = "Offline changes synced" }
        } catch { status = "Sync unavailable — changes remain on device" }
    }

    private func advance() async {
        await saveHole()
        guard errorMessage == nil else { return }
        if holeNumber < draft.expectedHoles { move(1) }
        else {
            do { _ = try await appState.api.complete(roundId: draft.id); status = "Round complete" }
            catch { errorMessage = AppState.message(for: error) }
        }
    }
}

struct ConflictState: Identifiable {
    let id = UUID()
    let holeNumber: Int
    let local: HoleUpsertRequest
    let server: Hole?
    let pendingID: UUID
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
