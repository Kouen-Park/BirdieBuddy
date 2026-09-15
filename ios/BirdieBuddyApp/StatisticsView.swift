import SwiftUI

struct StatisticsView: View {
    @EnvironmentObject private var appState: AppState
    @State private var statistics: OverviewStatistics?
    @State private var errorMessage: String?

    var body: some View {
        NavigationStack {
            Group {
                if let statistics {
                    List {
                        Section("Overview") {
                            LabeledContent("Rounds played", value: "\(statistics.roundsPlayed)")
                            LabeledContent("Average score", value: statistics.averageScore.formatted(.number.precision(.fractionLength(1))))
                            LabeledContent("Best score", value: "\(statistics.bestScore)")
                            LabeledContent("Average putts", value: statistics.averagePutts.formatted(.number.precision(.fractionLength(1))))
                            LabeledContent("GIR", value: "\(statistics.averageGirPercentage.formatted(.number.precision(.fractionLength(1))) )%")
                        }
                        Section("Recent rounds") {
                            ForEach(statistics.recentRounds) { round in
                                Text("\(round.courseName) · \(round.totalScore) · \(round.date)")
                            }
                        }
                    }
                } else if let errorMessage { ContentUnavailableView("Could not load statistics", systemImage: "chart.bar.xaxis", description: Text(errorMessage)) }
                else { ProgressView("Loading statistics…") }
            }
            .navigationTitle("Statistics")
            .refreshable { await load() }
            .task { await load() }
        }
    }

    private func load() async {
        do { statistics = try await appState.api.statistics() }
        catch { errorMessage = AppState.message(for: error) }
    }
}
