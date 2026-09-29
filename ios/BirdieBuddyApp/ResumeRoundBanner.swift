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
                    HStack(alignment: .center, spacing: 14) {
                        VStack(alignment: .leading, spacing: 6) {
                            Text("Round in progress")
                                .font(BirdieTheme.mono(11))
                                .foregroundStyle(BirdieTheme.sun)
                            Text(draft.courseName)
                                .font(BirdieTheme.display(23))
                                .foregroundStyle(BirdieTheme.paper)
                            Text("\(draft.holesPlayed) of \(draft.expectedHoles) holes saved")
                                .font(BirdieTheme.body(13))
                                .foregroundStyle(BirdieTheme.paper.opacity(0.72))
                            Text("Continue this round")
                                .font(BirdieTheme.body(13, weight: .semibold))
                                .foregroundStyle(BirdieTheme.sun)
                            if let sync = appState.roundSyncStatuses[draft.id], sync != .synced {
                                Label(LocalizedStringKey(sync.label), systemImage: "icloud.and.arrow.up")
                                    .font(BirdieTheme.body(12, weight: .semibold))
                                    .foregroundStyle(BirdieTheme.sun)
                            }
                        }
                        Spacer(minLength: 0)
                        Image(systemName: "arrow.up.right")
                            .font(.headline)
                            .foregroundStyle(BirdieTheme.sun)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(20)
                    .background(BirdieTheme.night, in: RoundedRectangle(cornerRadius: 22))
                    .padding(.horizontal, 16)
                    .padding(.top, 12)
                    .accessibilityElement(children: .combine)
                    .accessibilityHint("Reopens the round you have not finished")
                }
            } else if loadFailed {
                Button("Retry loading round") { Task { await load() } }
                    .padding(.horizontal, 16)
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
