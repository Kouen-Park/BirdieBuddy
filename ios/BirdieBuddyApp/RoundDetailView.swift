import SwiftUI

struct RoundDetailView: View {
    @EnvironmentObject private var appState: AppState
    let round: RoundSummary
    @State private var detail: RoundDetail?
    @State private var errorMessage: String?

    var body: some View {
        Group {
            if let detail {
                List(detail.holes.sorted { $0.holeNumber < $1.holeNumber }) { hole in
                    HStack {
                        Text("Hole \(hole.holeNumber)").frame(width: 70, alignment: .leading)
                        Spacer()
                        Text("Score \(hole.score)")
                        Text("Putts \(hole.putts)").foregroundStyle(.secondary)
                    }
                }
            } else if let errorMessage {
                ContentUnavailableView("Could not load round", systemImage: "exclamationmark.triangle", description: Text(errorMessage))
            } else { ProgressView("Loading round…") }
        }
        .navigationTitle(round.courseName)
        .task {
            do { detail = try await appState.api.round(id: round.id) }
            catch { errorMessage = AppState.message(for: error) }
        }
    }
}
