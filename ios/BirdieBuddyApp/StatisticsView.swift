import Charts
import SwiftUI

struct StatisticsView: View {
    @EnvironmentObject private var appState: AppState

    @State private var statistics: OverviewStatistics?
    @State private var courses: [CourseSummary] = []
    @State private var selection = RoundFilterSelection()
    @State private var errorMessage: String?
    @State private var isLoading = false

    private var filter: RoundFilter { selection.asFilter }

    var body: some View {
        NavigationStack {
            List {
                filterSection

                if let statistics {
                    if statistics.roundsPlayed == 0 {
                        Section {
                            ContentUnavailableView("No rounds match these filters",
                                                   systemImage: "line.3.horizontal.decrease.circle")
                        }
                    } else {
                        overviewSection(statistics)
                        roundLengthSection(statistics)
                        trendSection(statistics)
                        insightSection(statistics)
                        recentSection(statistics)
                    }
                } else if let errorMessage {
                    Section {
                        ContentUnavailableView("Could not load statistics", systemImage: "chart.bar.xaxis",
                                               description: Text(errorMessage))
                    }
                } else {
                    Section { ProgressView("Loading statistics…") }
                }
            }
            .navigationTitle("Statistics")
            .refreshable { await load() }
            .task {
                if courses.isEmpty { courses = (try? await appState.api.courses()) ?? [] }
                await load()
            }
            .onChange(of: filter) { _, _ in Task { await load() } }
        }
    }

    // MARK: - Sections

    private var filterSection: some View {
        Section("Filters") {
            RoundFilterControls(selection: $selection, courses: courses, isUpdating: isLoading)
        }
    }

    private func overviewSection(_ statistics: OverviewStatistics) -> some View {
        Section("Overview") {
            LabeledContent("Rounds played", value: "\(statistics.roundsPlayed)")
            LabeledContent("Average score", value: Self.number(statistics.averageScore))
            LabeledContent("Best score", value: "\(statistics.bestScore)")
            LabeledContent("Average putts", value: Self.number(statistics.averagePutts))
            if let perHole = statistics.averagePuttsPerHole, perHole > 0 {
                LabeledContent("Putts per hole", value: Self.number(perHole, digits: 2))
            }
            LabeledContent("GIR", value: Self.percent(statistics.averageGirPercentage))
            if let fairway = statistics.averageFairwayPercentage {
                LabeledContent("Fairways hit", value: Self.percent(fairway))
            }
            if let five = statistics.recentFiveScoreToPar {
                LabeledContent("Last 5 to par", value: Self.signed(five))
            }
            if let ten = statistics.recentTenScoreToPar {
                LabeledContent("Last 10 to par", value: Self.signed(ten))
            }
        }
    }

    @ViewBuilder
    private func roundLengthSection(_ statistics: OverviewStatistics) -> some View {
        if let lengths = statistics.byRoundLength, !lengths.isEmpty {
            Section("9 holes vs 18 holes") {
                ForEach(lengths) { length in
                    VStack(alignment: .leading, spacing: 4) {
                        Text("\(length.holeCount) holes").font(.headline)
                        Text("\(length.roundsPlayed) rounds · avg \(Self.number(length.averageScore)) · best \(length.bestScore)")
                            .font(.subheadline).foregroundStyle(.secondary)
                        Text("To par \(Self.signed(length.averageScoreToPar))")
                            .font(.subheadline)
                    }
                    .padding(.vertical, 2)
                    .accessibilityElement(children: .combine)
                }
            }
        }
    }

    @ViewBuilder
    private func trendSection(_ statistics: OverviewStatistics) -> some View {
        let score = statistics.scoreToParPerHoleTrend ?? statistics.scoreTrend ?? []
        let putts = statistics.puttsPerHoleTrend ?? statistics.puttsTrend ?? []
        let gir = statistics.girTrend ?? []

        if !score.isEmpty || !putts.isEmpty || !gir.isEmpty {
            Section("Trends") {
                TrendChart(title: "Score", points: score)
                TrendChart(title: "Putts", points: putts)
                TrendChart(title: "GIR %", points: gir)
            }
        }
    }

    @ViewBuilder
    private func insightSection(_ statistics: OverviewStatistics) -> some View {
        if let insights = statistics.insights, !insights.isEmpty {
            Section("What to work on") {
                ForEach(insights) { insight in
                    VStack(alignment: .leading, spacing: 4) {
                        Text(insight.title).font(.headline)
                        Text(insight.evidence).font(.subheadline).foregroundStyle(.secondary)
                        Text(insight.recommendation).font(.subheadline)
                    }
                    .padding(.vertical, 2)
                    .accessibilityElement(children: .combine)
                }
            }
        }
    }

    private func recentSection(_ statistics: OverviewStatistics) -> some View {
        Section("Recent rounds") {
            ForEach(statistics.recentRounds) { round in
                NavigationLink {
                    RoundDetailView(round: round)
                } label: {
                    VStack(alignment: .leading, spacing: 2) {
                        Text(round.courseName).font(.headline)
                        Text("\(round.date) · \(round.totalScore) (\(Self.signedInt(round.scoreToPar)))")
                            .font(.subheadline).foregroundStyle(.secondary)
                    }
                    .accessibilityElement(children: .combine)
                }
            }
        }
    }

    // MARK: - Loading

    private func load() async {
        isLoading = true
        errorMessage = nil
        do { statistics = try await appState.api.statistics(filter: filter) }
        catch { errorMessage = AppState.message(for: error) }
        isLoading = false
    }

    // MARK: - Formatting

    static func number(_ value: Double, digits: Int = 1) -> String {
        value.formatted(.number.precision(.fractionLength(digits)))
    }

    static func percent(_ value: Double) -> String {
        "\(number(value))%"
    }

    static func signed(_ value: Double) -> String {
        (value >= 0 ? "+" : "") + number(value)
    }

    static func signedInt(_ value: Int) -> String {
        (value >= 0 ? "+" : "") + "\(value)"
    }
}

/// A compact line chart for one trend series. Swift Charts ships with the SDK, so
/// this adds no third-party dependency.
private struct TrendChart: View {
    let title: LocalizedStringKey
    let points: [TrendPoint]

    // A fixed-height chart looks stranded beside text that has doubled in size.
    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 140

    var body: some View {
        let plotted = points.compactMap { point -> (Date, Double)? in
            guard let day = point.day else { return nil }
            return (day, point.value)
        }

        if plotted.count >= 2 {
            VStack(alignment: .leading, spacing: 6) {
                Text(title).font(.subheadline.bold())
                Chart {
                    ForEach(Array(plotted.enumerated()), id: \.offset) { _, entry in
                        LineMark(x: .value("Date", entry.0), y: .value("Value", entry.1))
                        // Per-point labels let VoiceOver swipe through the series
                        // instead of hearing only "chart, 12 rounds plotted".
                        PointMark(x: .value("Date", entry.0), y: .value("Value", entry.1))
                            .accessibilityLabel(entry.0.formatted(date: .abbreviated, time: .omitted))
                            .accessibilityValue(StatisticsView.number(entry.1, digits: 2))
                    }
                }
                .frame(height: min(chartHeight, 220))
                .accessibilityLabel(title)
            }
            .padding(.vertical, 4)
        }
    }
}
