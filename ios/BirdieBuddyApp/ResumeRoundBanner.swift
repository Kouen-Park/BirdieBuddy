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
            }
        }
        .task { await load() }
    }

    private func load() async {
        // Best effort. Offline this finds nothing: only the round's pending writes
        // are cached on the device, not the round itself, so resuming without a
        // connection needs local draft persistence that this does not add.
        draft = try? await appState.api.activeDraft()
        await appState.refreshSyncStatuses()
    }
}
