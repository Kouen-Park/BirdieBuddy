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
                ContentUnavailableView("Could not open this round", systemImage: "exclamationmark.triangle",
                                       description: Text(errorMessage))
            } else {
                ProgressView("Opening round…")
            }
        }
        .task {
            guard draft == nil else { return }
            do {
                draft = try await appState.api.round(id: roundId)
            } catch {
                errorMessage = AppState.message(for: error)
            }
        }
    }
}
