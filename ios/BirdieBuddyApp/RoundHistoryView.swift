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
            // Above the List: this screen shows completed rounds, so the round still
            // in progress is not among them and had no way to be reached or resumed.
            VStack(spacing: 0) {
                ResumeRoundBanner()
                List {
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
            .navigationTitle("Rounds")
            .refreshable { await reload() }
            .task {
                if courses.isEmpty { courses = (try? await appState.api.courses()) ?? [] }
                if rounds.isEmpty { await reload() }
                await appState.refreshSyncStatuses()
            }
            .onChange(of: selection) { _, _ in Task { await reload() } }
            }
        }
    }

    private func row(_ round: RoundSummary) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(round.courseName).font(.headline)
            if round.isDraft {
                Text("In progress")
                    .font(.caption.bold())
                    .foregroundStyle(.orange)
            }
            Text("\(round.date) · \(round.holesPlayed)/\(round.expectedHoles) holes · \(round.tee)")
                .font(.subheadline).foregroundStyle(.secondary)
            Text("\(round.totalScore) (\(StatisticsView.signedInt(round.scoreToPar)))")
                .font(.title3.bold())
            if let syncStatus = appState.roundSyncStatuses[round.id], syncStatus != .synced {
                // LocalizedStringKey, not the raw String: a String argument skips the
                // catalog and the badge renders in English on a localized device.
                Label(LocalizedStringKey(syncStatus.label),
                      systemImage: syncStatus == .reviewRequired ? "exclamationmark.triangle" : "icloud.and.arrow.up")
                    .font(.caption)
                    .foregroundStyle(syncStatus == .attentionRequired || syncStatus == .reviewRequired ? .orange : .secondary)
            }
        }
        .padding(.vertical, 4)
        .accessibilityElement(children: .combine)
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
