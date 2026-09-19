import SwiftUI

/// Creates or renames a custom course.
///
/// Create sends all 18 holes because the server requires exactly 18 with unique
/// numbers. Edit only sends name and location: the server's course-update
/// contract does not accept hole or tee changes, so offering them here would
/// promise something the API cannot do.
struct CourseFormView: View {
    enum Mode: Equatable {
        case create
        case edit(courseId: Int)
    }

    struct HoleDraft: Identifiable, Equatable {
        let id: Int          // hole number, 1...18
        var par: Int
        var distance: Int
    }

    let mode: Mode
    let onSaved: () -> Void

    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss

    @State private var name: String
    @State private var location: String
    @State private var holes: [HoleDraft]
    @State private var isSaving = false
    @State private var errorMessage: String?

    init(mode: Mode, name: String = "", location: String = "", onSaved: @escaping () -> Void) {
        self.mode = mode
        self.onSaved = onSaved
        _name = State(initialValue: name)
        _location = State(initialValue: location)
        _holes = State(initialValue: (1...18).map { HoleDraft(id: $0, par: 4, distance: 0) })
    }

    private var isCreating: Bool { mode == .create }
    private var trimmedName: String { name.trimmingCharacters(in: .whitespacesAndNewlines) }
    private var trimmedLocation: String { location.trimmingCharacters(in: .whitespacesAndNewlines) }

    private var canSave: Bool {
        !isSaving
            && (2...160).contains(trimmedName.count)
            && (2...160).contains(trimmedLocation.count)
    }

    private var totalPar: Int { holes.reduce(0) { $0 + $1.par } }

    var body: some View {
        NavigationStack {
            Form {
                Section("Course") {
                    TextField("Course name", text: $name)
                        .accessibilityLabel("Course name")
                    TextField("Location", text: $location)
                        .accessibilityLabel("Course location")
                }

                if isCreating {
                    Section {
                        LabeledContent("Total par", value: "\(totalPar)")
                    } header: {
                        Text("Holes")
                    } footer: {
                        Text("A custom course needs all 18 holes. Distances are optional.")
                    }

                    ForEach($holes) { $hole in
                        Section("Hole \(hole.id)") {
                            Stepper("Par: \(hole.par)", value: $hole.par, in: 3...6)
                                .accessibilityLabel("Par for hole \(hole.id)")
                                .accessibilityValue("\(hole.par)")
                            TextField("Distance", value: $hole.distance, format: .number)
                                .keyboardType(.numberPad)
                                .accessibilityLabel("Distance for hole \(hole.id)")
                        }
                    }
                } else {
                    Section {
                        Text("Holes and tees cannot be edited after a course is created.")
                            .font(.footnote)
                            .foregroundStyle(.secondary)
                    }
                }

                if let errorMessage {
                    Section {
                        Label(errorMessage, systemImage: "exclamationmark.triangle.fill")
                            .foregroundStyle(.red)
                    }
                }
            }
            .navigationTitle(isCreating ? "New course" : "Edit course")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }.disabled(isSaving)
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button(isSaving ? "Saving…" : "Save") { Task { await save() } }
                        .disabled(!canSave)
                }
            }
        }
    }

    private func save() async {
        isSaving = true
        errorMessage = nil
        do {
            switch mode {
            case .create:
                let payload = holes.map {
                    CourseHoleCreateRequest(holeNumber: $0.id, par: $0.par, distance: max(0, $0.distance))
                }
                _ = try await appState.api.createCourse(
                    name: trimmedName, location: trimmedLocation, holes: payload)
            case .edit(let courseId):
                try await appState.api.updateCourse(
                    id: courseId, name: trimmedName, location: trimmedLocation)
            }
            isSaving = false
            onSaved()
            dismiss()
        } catch {
            errorMessage = AppState.message(for: error)
            isSaving = false
        }
    }
}
