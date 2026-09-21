import SwiftUI

struct CourseBrowserView: View {
    @EnvironmentObject private var appState: AppState
    @State private var courses: [CourseSummary] = []
    @State private var isLoading = true
    @State private var errorMessage: String?
    @State private var isAddingCourse = false

    var body: some View {
        NavigationStack {
            // Above the Group on purpose: when the course list fails to load — which
            // is exactly the offline case after an interrupted round — the Group is
            // replaced entirely, and the way back into that round must not vanish
            // with it.
            VStack(spacing: 0) {
                ResumeRoundBanner()
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
                                if course.isCustom {
                                    Text("My course").font(.caption).foregroundStyle(.secondary)
                                }
                            }
                            .padding(.vertical, 4)
                            .accessibilityElement(children: .combine)
                        }
                    }
                    .navigationDestination(for: CourseSummary.self) { course in
                        CourseDetailView(course: course)
                    }
                }
                }
            }
            .navigationTitle("Courses")
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Sign out") { Task { await appState.signOut() } }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        isAddingCourse = true
                    } label: {
                        Label("Add course", systemImage: "plus")
                    }
                    .accessibilityLabel("Add a custom course")
                }
            }
            .sheet(isPresented: $isAddingCourse) {
                CourseFormView(mode: .create) {
                    Task { await loadCourses() }
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
