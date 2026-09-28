import SwiftUI

/// Opens live scoring for a round identified only by its id.
///
/// The round list and the resume banner both hold a `RoundSummary`, but scoring
/// needs the full round with its holes. Fetching that inside the destination keeps
/// both entry points to a plain `NavigationLink` — no loading state to juggle
/// before navigating, and the request happens only when the golfer actually goes
/// in, which matters while the API measures seconds per call.
struct LiveRoundLoader: View {
    @EnvironmentObject private var appState: AppState

    let roundId: Int

    @State private var draft: RoundDraft?
    @State private var errorMessage: String?

    var body: some View {
        Group {
            if let draft {
                LiveRoundView(draft: draft)
            } else if let errorMessage {
                ContentUnavailableView {
                    Label("Could not open this round", systemImage: "exclamationmark.triangle")
                } description: {
                    Text(errorMessage)
                } actions: {
                    Button("Retry") { Task { await load() } }
                }
            } else {
                ProgressView("Opening round…")
            }
        }
        .task { await load() }
    }

    private func load() async {
        guard draft == nil else { return }
        errorMessage = nil
        if let userId = appState.user?.id,
           let pending = try? await appState.roundPersistence.pending(userId: userId, roundId: roundId),
           !pending.isEmpty,
           let cached = try? await appState.roundPersistence.cachedDraft(userId: userId, roundId: roundId) {
            draft = cached
            return
        }
        do {
            let loaded = try await appState.api.round(id: roundId)
            if let userId = appState.user?.id {
                try await appState.roundPersistence.cacheDraft(userId: userId, draft: loaded)
            }
            draft = loaded
        } catch {
            if let userId = appState.user?.id,
               let cached = try? await appState.roundPersistence.cachedDraft(userId: userId, roundId: roundId) {
                draft = cached
            } else {
                errorMessage = AppState.message(for: error)
            }
        }
    }
}
