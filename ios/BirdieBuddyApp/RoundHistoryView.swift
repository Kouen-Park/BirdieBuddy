import SwiftUI

struct RoundHistoryView: View {
    @EnvironmentObject private var appState: AppState

    @State private var rounds: [RoundSummary] = []
    @State private var courses: [CourseSummary] = []
    @State private var selection = RoundFilterSelection()
    @State private var nextCursor: Int?
    @State private var hasMore = true
    @State private var isLoadingPage = false
    @State private var isReloading = false
    @State private var errorMessage: String?

    private static let pageSize = 20

    var body: some View {
        NavigationStack {
            List {
                // Scrolls with the page so an enlarged banner cannot crowd out
                // the filters and completed rounds at accessibility text sizes.
                ResumeRoundBanner()
                    .listRowInsets(EdgeInsets())
                    .listRowBackground(Color.clear)
                    .listRowSeparator(.hidden)
                BirdiePageHeader("Rounds")
                    .listRowBackground(Color.clear)
                Section("Filters") {
                    RoundFilterControls(selection: $selection, courses: courses, isUpdating: isReloading)
                }

                if let errorMessage {
                    Section {
                        ContentUnavailableView("Could not load rounds", systemImage: "wifi.exclamationmark",
                                               description: Text(errorMessage))
                    }
                } else if rounds.isEmpty && !isLoadingPage && !isReloading {
                    Section {
                        ContentUnavailableView(
                            selection.asFilter.isActive ? "No rounds match these filters" : "No completed rounds",
                            systemImage: "flag")
                    }
                } else {
                    Section {
                        ForEach(rounds) { round in
                            NavigationLink {
                                // A draft is still being played, and the server does
                                // include it in this list. Sending it to the
                                // read-only summary is what made an interrupted
                                // round impossible to continue; the web client
                                // branches on status here for the same reason.
                                if round.isDraft {
                                    LiveRoundLoader(roundId: round.id)
                                } else {
                                    RoundDetailView(round: round)
                                }
                            } label: {
                                row(round)
                            }
                            .listRowBackground(Color.clear)
                            .listRowSeparator(.hidden)
                            .onAppear {
                                // The last row coming into view is the paging trigger.
                                if round.id == rounds.last?.id { Task { await loadNextPage() } }
                            }
                        }
                        if isLoadingPage {
                            HStack {
                                ProgressView().controlSize(.small)
                                Text("Loading more…").foregroundStyle(.secondary)
                            }
                        }
                    } header: {
                        Text(hasMore ? "Rounds" : "Rounds (all loaded)")
                    }
                }
            }
            .birdieListStyle()
            .birdieNavigationBrand()
            .refreshable { await reload() }
            .task {
                if courses.isEmpty { courses = (try? await appState.api.courses()) ?? [] }
                if rounds.isEmpty { await reload() }
                await appState.refreshSyncStatuses()
            }
            .onChange(of: selection) { _, _ in Task { await reload() } }
        }
    }

    private func row(_ round: RoundSummary) -> some View {
        BirdieRoundRow(round: round, syncStatus: appState.roundSyncStatuses[round.id])
    }

    // MARK: - Paging

    private func reload() async {
        isReloading = true
        errorMessage = nil
        nextCursor = nil
        hasMore = true
        do {
            let page = try await appState.api.roundsPage(cursor: nil, limit: Self.pageSize, filter: selection.asFilter)
            rounds = page.items
            nextCursor = page.nextCursor
            hasMore = page.nextCursor != nil
        } catch {
            errorMessage = AppState.message(for: error)
            rounds = []
            hasMore = false
        }
        isReloading = false
        await appState.refreshSyncStatuses()
    }

    private func loadNextPage() async {
        guard hasMore, !isLoadingPage, !isReloading, let cursor = nextCursor else { return }
        isLoadingPage = true
        do {
            let page = try await appState.api.roundsPage(cursor: cursor, limit: Self.pageSize, filter: selection.asFilter)
            // Guard against a duplicate append if the trigger fires twice.
            let existing = Set(rounds.map(\.id))
            rounds.append(contentsOf: page.items.filter { !existing.contains($0.id) })
            nextCursor = page.nextCursor
            hasMore = page.nextCursor != nil
        } catch {
            // A failed page must not wipe the rounds already on screen.
            errorMessage = AppState.message(for: error)
            hasMore = false
        }
        isLoadingPage = false
    }
}

struct BirdieRoundRow: View {
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    let round: RoundSummary
    var syncStatus: RoundSyncStatus?

    var body: some View {
        let layout = dynamicTypeSize.isAccessibilitySize
            ? AnyLayout(VStackLayout(alignment: .leading, spacing: 12))
            : AnyLayout(HStackLayout(alignment: .top, spacing: 12))
        layout {
            VStack(alignment: .leading, spacing: 5) {
                Text(round.courseName)
                    .font(BirdieTheme.body(17, weight: .semibold))
                    .foregroundStyle(BirdieTheme.ink)
                Text("\(round.date) · \(round.holesPlayed)/\(round.expectedHoles) holes · \(round.tee)")
                    .font(BirdieTheme.body(13))
                    .foregroundStyle(BirdieTheme.muted)
                if round.isDraft {
                    Text("In progress")
                        .font(BirdieTheme.body(13, weight: .semibold))
                        .foregroundStyle(BirdieTheme.danger)
                }
                if let syncStatus, syncStatus != .synced {
                    Label(LocalizedStringKey(syncStatus.label),
                          systemImage: syncStatus == .reviewRequired ? "exclamationmark.triangle" : "icloud.and.arrow.up")
                        .font(BirdieTheme.body(13))
                        .foregroundStyle(BirdieTheme.danger)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            Text("\(round.totalScore) (\(StatisticsView.signedInt(round.scoreToPar)))")
                .font(BirdieTheme.mono(17))
                .foregroundStyle(BirdieTheme.ink)
                .fixedSize(horizontal: true, vertical: false)
        }
        .birdieCard()
        .accessibilityElement(children: .combine)
    }

}
