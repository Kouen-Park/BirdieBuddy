import SwiftUI

struct CourseDetailView: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss
    let course: CourseSummary
    @State private var detail: CourseDetail?
    @State private var selectedTeeId: Int?
    @State private var draft: RoundDraft?
    @State private var errorMessage: String?
    @State private var isLoading = true
    @State private var isEditing = false
    @State private var isConfirmingDelete = false
    @State private var isDeleting = false

    var body: some View {
        Group {
            if let draft {
                LiveRoundView(draft: draft)
            } else if isLoading {
                ProgressView("Loading course…")
            } else if let detail {
                Form {
                    Section("Course") {
                        LabeledContent("Location", value: detail.location)
                        Picker("Tee", selection: $selectedTeeId) {
                            ForEach(detail.tees) { tee in Text(tee.name).tag(Optional(tee.id)) }
                        }
                    }
                    if let errorMessage { Text(errorMessage).foregroundStyle(.red) }
                    Section {
                        Button("Start round") {
                            Task {
                                do { draft = try await appState.api.startDraft(courseId: course.id, teeId: selectedTeeId) }
                                catch { errorMessage = AppState.message(for: error) }
                            }
                        }
                        .disabled(selectedTeeId == nil)
                    }

                    if detail.isCustom {
                        Section("My course") {
                            Button("Edit name and location") { isEditing = true }
                                .accessibilityLabel("Edit course name and location")
                            Button("Delete course", role: .destructive) { isConfirmingDelete = true }
                                .disabled(isDeleting)
                                .accessibilityLabel("Delete this course")
                        }
                    }
                }
            } else {
                ContentUnavailableView("Could not load course", systemImage: "wifi.exclamationmark", description: Text(errorMessage ?? "Try again."))
            }
        }
        .navigationTitle(detail?.name ?? course.name)
        .sheet(isPresented: $isEditing) {
            CourseFormView(
                mode: .edit(courseId: course.id),
                name: detail?.name ?? course.name,
                location: detail?.location ?? course.location
            ) {
                Task { await load() }
            }
        }
        .alert("Delete this course?", isPresented: $isConfirmingDelete) {
            Button("Cancel", role: .cancel) {}
            Button("Delete", role: .destructive) { Task { await deleteCourse() } }
        } message: {
            Text("A course used by a recorded round cannot be deleted.")
        }
        .task { await load() }
    }

    private func load() async {
        do {
            detail = try await appState.api.course(id: course.id)
            if selectedTeeId == nil { selectedTeeId = detail?.tees.first?.id }
        } catch { errorMessage = AppState.message(for: error) }
        isLoading = false
    }

    private func deleteCourse() async {
        isDeleting = true
        errorMessage = nil
        do {
            try await appState.api.deleteCourse(id: course.id)
            dismiss()
        } catch {
            errorMessage = AppState.message(for: error)
        }
        isDeleting = false
    }
}
