import SwiftUI

struct RoundHistoryView: View {
    @EnvironmentObject private var appState: AppState
    @State private var rounds: [RoundSummary] = []
    @State private var errorMessage: String?
    @State private var isLoading = true

    var body: some View {
        NavigationStack {
            Group {
                if isLoading { ProgressView("Loading rounds…") }
                else if let errorMessage { ContentUnavailableView("Could not load rounds", systemImage: "wifi.exclamationmark", description: Text(errorMessage)) }
                else if rounds.isEmpty { ContentUnavailableView("No completed rounds", systemImage: "flag") }
                else {
                    List(rounds) { round in
                        NavigationLink {
                            RoundDetailView(round: round)
                        } label: {
                            VStack(alignment: .leading, spacing: 4) {
                                Text(round.courseName).font(.headline)
                                Text("\(round.date) · \(round.holesPlayed)/\(round.expectedHoles) holes · \(round.tee)")
                                    .font(.subheadline).foregroundStyle(.secondary)
                                Text("\(round.totalScore) (\(round.scoreToPar >= 0 ? "+" : "")\(round.scoreToPar))")
                                    .font(.title3.bold())
                            }
                            .padding(.vertical, 4)
                        }
                    }
                }
            }
            .navigationTitle("Rounds")
            .refreshable { await load() }
            .task { await load() }
        }
    }

    private func load() async {
        isLoading = rounds.isEmpty; errorMessage = nil
        do { rounds = try await appState.api.rounds() }
        catch { errorMessage = AppState.message(for: error) }
        isLoading = false
    }
}
