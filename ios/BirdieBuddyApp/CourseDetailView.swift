import SwiftUI

struct CourseDetailView: View {
    @EnvironmentObject private var appState: AppState
    let course: CourseSummary
    @State private var detail: CourseDetail?
    @State private var selectedTeeId: Int?
    @State private var draft: RoundDraft?
    @State private var errorMessage: String?
    @State private var isLoading = true

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
                }
            } else {
                ContentUnavailableView("Could not load course", systemImage: "wifi.exclamationmark", description: Text(errorMessage ?? "Try again."))
            }
        }
        .navigationTitle(course.name)
        .task {
            do {
                detail = try await appState.api.course(id: course.id)
                selectedTeeId = detail?.tees.first?.id
            } catch { errorMessage = AppState.message(for: error) }
            isLoading = false
        }
    }
}
