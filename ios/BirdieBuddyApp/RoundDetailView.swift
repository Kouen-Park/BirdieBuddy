import SwiftUI

struct RoundDetailView: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss

    let round: RoundSummary

    @State private var detail: RoundDetail?
    @State private var courseTees: [CourseTee] = []
    @State private var editedDate = Date.now
    @State private var editedTeeId: Int?
    @State private var errorMessage: String?
    @State private var statusMessage: String?
    @State private var isSaving = false
    @State private var isConfirmingDelete = false

    private var hasChanges: Bool {
        guard let detail else { return false }
        let originalDate = APIClient.dayFormatter.date(from: detail.date)
        let dateChanged = originalDate.map {
            !Calendar.current.isDate($0, inSameDayAs: editedDate)
        } ?? true
        return dateChanged || editedTeeId != detail.courseTeeId
    }

    var body: some View {
        Group {
            if let detail {
                List {
                    Section("Round") {
                        DatePicker("Date", selection: $editedDate, displayedComponents: .date)
                            .accessibilityLabel("Round date")
                        if courseTees.isEmpty {
                            LabeledContent("Tee", value: detail.tee)
                        } else {
                            Picker("Tee", selection: $editedTeeId) {
                                ForEach(courseTees) { tee in
                                    Text(tee.name).tag(Optional(tee.id))
                                }
                            }
                        }
                        Button(isSaving ? "Saving…" : "Save changes") { Task { await save() } }
                            .disabled(isSaving || !hasChanges)
                            .accessibilityLabel("Save round changes")
                    }

                    if let statusMessage {
                        Section { Text(statusMessage).foregroundStyle(.secondary) }
                    }
                    if let errorMessage {
                        Section {
                            Label(errorMessage, systemImage: "exclamationmark.triangle.fill")
                                .foregroundStyle(.red)
                        }
                    }

                    Section("Holes") {
                        ForEach(detail.holes.sorted { $0.holeNumber < $1.holeNumber }) { hole in
                            HStack {
                                Text("Hole \(hole.holeNumber)").frame(width: 70, alignment: .leading)
                                Spacer()
                                Text("Score \(hole.score)")
                                Text("Putts \(hole.putts)").foregroundStyle(.secondary)
                            }
                            .accessibilityElement(children: .combine)
                        }
                    }

                    Section {
                        Button("Delete round", role: .destructive) { isConfirmingDelete = true }
                            .disabled(isSaving)
                            .accessibilityLabel("Delete this round")
                    }
                }
            } else if let errorMessage {
                ContentUnavailableView("Could not load round", systemImage: "exclamationmark.triangle", description: Text(errorMessage))
            } else { ProgressView("Loading round…") }
        }
        .navigationTitle(round.courseName)
        .alert("Delete this round?", isPresented: $isConfirmingDelete) {
            Button("Cancel", role: .cancel) {}
            Button("Delete", role: .destructive) { Task { await deleteRound() } }
        } message: {
            Text("This permanently removes the round and its hole scores.")
        }
        .task { await load() }
    }

    private func load() async {
        do {
            let loaded = try await appState.api.round(id: round.id)
            detail = loaded
            editedDate = APIClient.dayFormatter.date(from: loaded.date) ?? .now
            editedTeeId = loaded.courseTeeId
            // Tees are only needed to offer alternatives; failing to load them
            // leaves the current tee shown read-only rather than blocking the screen.
            if let course = try? await appState.api.course(id: loaded.courseId) {
                courseTees = course.tees
            }
        } catch {
            errorMessage = AppState.message(for: error)
        }
    }

    private func save() async {
        guard let detail else { return }
        isSaving = true
        errorMessage = nil
        statusMessage = nil
        do {
            let teeName = courseTees.first { $0.id == editedTeeId }?.name
            try await appState.api.updateRound(
                id: detail.id,
                date: editedDate,
                courseTeeId: editedTeeId,
                tee: teeName,
                expectedUpdatedAt: detail.updatedAt)
            await load()
            statusMessage = "Round updated"
        } catch {
            errorMessage = AppState.message(for: error)
        }
        isSaving = false
    }

    private func deleteRound() async {
        isSaving = true
        errorMessage = nil
        if await appState.deleteRound(id: round.id) {
            dismiss()
        } else {
            errorMessage = appState.errorMessage
        }
        isSaving = false
    }
}
