import SwiftUI

struct PracticeView: View {
    @EnvironmentObject private var appState: AppState
    @State private var sessions: [PracticeSession] = []
    @State private var focus = "Putting"
    @State private var drill = "Distance control"
    @State private var minutes = 30
    @State private var isStarting = false
    @State private var errorMessage: String?

    var body: some View {
        NavigationStack {
            List {
                BirdiePageHeader("Practice")
                    .listRowBackground(Color.clear)
                Section("Start practice") {
                    VStack(alignment: .leading, spacing: 6) {
                        Text("Focus").font(BirdieTheme.body(13)).foregroundStyle(BirdieTheme.muted)
                        TextField("Focus", text: $focus).accessibilityLabel("Focus")
                    }
                    VStack(alignment: .leading, spacing: 6) {
                        Text("Drill").font(BirdieTheme.body(13)).foregroundStyle(BirdieTheme.muted)
                        TextField("Drill", text: $drill).accessibilityLabel("Drill")
                    }
                    Stepper("Minutes: \(minutes)", value: $minutes, in: 1...240)
                    Button(isStarting ? "Starting…" : "Start session") {
                        Task {
                            isStarting = true
                            do {
                                let session = try await appState.api.startPractice(focusCode: focus, drillTitle: drill, minutes: minutes)
                                sessions.insert(session, at: 0)
                            } catch { errorMessage = AppState.message(for: error) }
                            isStarting = false
                        }
                    }
                    .disabled(isStarting || focus.trimmingCharacters(in: .whitespaces).isEmpty || drill.trimmingCharacters(in: .whitespaces).isEmpty)
                    .buttonStyle(.borderedProminent)
                    .controlSize(.large)
                    .tint(BirdieTheme.fairwayDark)
                }
                if let errorMessage { Text(errorMessage).foregroundStyle(BirdieTheme.danger) }
                Section("Recent sessions") {
                    if sessions.isEmpty {
                        ContentUnavailableView("No practice sessions yet", systemImage: "figure.golf",
                            description: Text("Start a session to keep track of your practice."))
                    }
                    ForEach(sessions) { session in
                        HStack(alignment: .top, spacing: 14) {
                            Text("\(sessions.firstIndex(where: { $0.id == session.id }).map { $0 + 1 } ?? 1)")
                                .font(BirdieTheme.mono(24))
                                .foregroundStyle(BirdieTheme.fairwayDark)
                            VStack(alignment: .leading, spacing: 4) {
                            Text(session.drillTitle)
                                .font(BirdieTheme.display(20))
                                .foregroundStyle(BirdieTheme.ink)
                            Text("\(session.focusCode) · \(session.minutes) minutes")
                                .font(BirdieTheme.body(13)).foregroundStyle(BirdieTheme.muted)
                            Text(session.completedAt == nil ? "In progress" : "Completed")
                                .font(BirdieTheme.body(13, weight: .semibold))
                                .foregroundStyle(session.completedAt == nil ? BirdieTheme.danger : BirdieTheme.fairway)
                            }
                        }
                        .birdieCard()
                        .accessibilityElement(children: .combine)
                        .listRowBackground(Color.clear)
                        .listRowSeparator(.hidden)
                    }
                }
            }
            .birdieListStyle()
            .birdieNavigationBrand()
            .refreshable { await load() }
            .task { await load() }
        }
    }

    private func load() async {
        do { sessions = try await appState.api.practiceSessions() }
        catch { errorMessage = AppState.message(for: error) }
    }
}
