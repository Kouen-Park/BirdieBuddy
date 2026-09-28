import SwiftUI

/// Prompt to continue a round that is still a draft.
///
/// A round interrupted by a force quit, a crash or just leaving the screen was only
/// reachable as a row in the round list, and that row opened the read-only summary —
/// so scoring could not be continued at all. This makes the unfinished round
/// impossible to miss, the way the web client's dashboard does, and reads the same
/// endpoint: `/rounds/page?status=Draft&limit=1`.
///
/// Renders nothing when there is no draft, so it can sit above any screen.
struct ResumeRoundBanner: View {
    @EnvironmentObject private var appState: AppState

    @State private var draft: RoundSummary?
    @State private var loadFailed = false

    var body: some View {
        Group {
            if let draft {
                NavigationLink {
                    LiveRoundLoader(roundId: draft.id)
                } label: {
                    VStack(alignment: .leading, spacing: 6) {
                        Text("Round in progress")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        Text(draft.courseName)
                            .font(.headline)
                        Text("\(draft.holesPlayed) of \(draft.expectedHoles) holes saved")
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                        if let sync = appState.roundSyncStatuses[draft.id], sync != .synced {
                            Label(LocalizedStringKey(sync.label), systemImage: "icloud.and.arrow.up")
                                .font(.caption)
                                .foregroundStyle(.orange)
                        }
                        Text("Continue this round")
                            .font(.subheadline.bold())
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding()
                    .accessibilityElement(children: .combine)
                    .accessibilityHint("Reopens the round you have not finished")
                }
                Divider()
            } else if loadFailed {
                Button("Retry loading round") { Task { await load() } }
            }
        }
        .task(id: appState.user?.id) { await load() }
    }

    private func load() async {
        guard let userId = appState.user?.id else { draft = nil; return }
        loadFailed = false
        do {
            draft = try await appState.api.activeDraft()
        } catch {
            if let cached = try? await appState.roundPersistence.latestCachedDraft(userId: userId) {
                draft = RoundSummary(id: cached.id, courseId: cached.courseId,
                    courseName: cached.courseName, date: cached.date, tee: cached.tee,
                    totalScore: cached.holes.reduce(0) { $0 + $1.score },
                    scoreToPar: cached.holes.reduce(0) { $0 + $1.score - $1.par },
                    status: cached.status, holesPlayed: cached.holes.count,
                    expectedHoles: cached.expectedHoles)
            } else {
                draft = nil
                loadFailed = true
            }
        }
        await appState.refreshSyncStatuses()
    }
}
