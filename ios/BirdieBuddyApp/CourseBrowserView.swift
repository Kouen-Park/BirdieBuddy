import SwiftUI

struct CourseBrowserView: View {
    @EnvironmentObject private var appState: AppState
    @State private var courses: [CourseSummary] = []
    @State private var isLoading = true
    @State private var errorMessage: String?

    var body: some View {
        NavigationStack {
            Group {
                if isLoading {
                    ProgressView("Loading courses…")
                } else if let errorMessage {
                    ContentUnavailableView("Could not load courses", systemImage: "wifi.exclamationmark", description: Text(errorMessage))
                } else if courses.isEmpty {
                    ContentUnavailableView("No courses yet", systemImage: "flag", description: Text("Course data will appear here when it is available."))
                } else {
                    List(courses) { course in
                        NavigationLink(value: course) {
                            VStack(alignment: .leading, spacing: 4) {
                                Text(course.name).font(.headline)
                                Text(course.location).font(.subheadline).foregroundStyle(.secondary)
                            }
                            .padding(.vertical, 4)
                        }
                    }
                    .navigationDestination(for: CourseSummary.self) { course in
                        CourseDetailView(course: course)
                    }
                }
            }
            .navigationTitle("Courses")
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Sign out") { Task { await appState.signOut() } }
                }
            }
            .refreshable { await loadCourses() }
            .task { await loadCourses() }
        }
    }

    private func loadCourses() async {
        isLoading = courses.isEmpty
        errorMessage = nil
        do { courses = try await appState.api.courses() }
        catch { errorMessage = AppState.message(for: error) }
        isLoading = false
    }
}
