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
                Section("Start practice") {
                    TextField("Focus", text: $focus)
                    TextField("Drill", text: $drill)
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
                }
                if let errorMessage { Text(errorMessage).foregroundStyle(.red) }
                Section("Recent sessions") {
                    ForEach(sessions) { session in
                        VStack(alignment: .leading, spacing: 4) {
                            Text(session.drillTitle).font(.headline)
                            Text("\(session.focusCode) · \(session.minutes) minutes")
                                .font(.subheadline).foregroundStyle(.secondary)
                            Text(session.completedAt == nil ? "In progress" : "Completed")
                                .font(.caption).foregroundStyle(session.completedAt == nil ? .orange : .green)
                        }
                        .padding(.vertical, 4)
                    }
                }
            }
            .navigationTitle("Practice")
            .refreshable { await load() }
            .task { await load() }
        }
    }

    private func load() async {
        do { sessions = try await appState.api.practiceSessions() }
        catch { errorMessage = AppState.message(for: error) }
    }
}
