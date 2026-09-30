import SwiftUI

struct CourseBrowserView: View {
    @EnvironmentObject private var appState: AppState
    @State private var courses: [CourseSummary] = []
    @State private var isLoading = true
    @State private var errorMessage: String?
    @State private var isAddingCourse = false
    @State private var searchText = ""

    private var filteredCourses: [CourseSummary] {
        guard !searchText.isEmpty else { return courses }
        return courses.filter {
            $0.name.localizedCaseInsensitiveContains(searchText)
                || $0.location.localizedCaseInsensitiveContains(searchText)
        }
    }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: 0) {
                    // Keep resume available even when courses fail to load, and let
                    // the banner scroll at large accessibility text sizes.
                    ResumeRoundBanner()
                    VStack(alignment: .leading, spacing: 18) {
                        BirdiePageHeader("Courses", subtitle: "Courses you can select when adding a round.")
                        HStack {
                            Image(systemName: "magnifyingglass").foregroundStyle(BirdieTheme.muted)
                            TextField("Search by course or location", text: $searchText)
                                .textInputAutocapitalization(.never)
                                .accessibilityLabel("Search by course or location")
                        }
                        .birdieCard(padding: 16)

                        Text("Available courses")
                            .font(BirdieTheme.display(21))
                            .foregroundStyle(BirdieTheme.ink)
                        if isLoading {
                            ProgressView("Loading courses…").frame(maxWidth: .infinity)
                        } else if let errorMessage {
                            ContentUnavailableView {
                                Label("Could not load courses", systemImage: "wifi.exclamationmark")
                            } description: {
                                Text(errorMessage)
                            } actions: {
                                Button("Retry") { Task { await loadCourses() } }
                            }
                        } else if filteredCourses.isEmpty {
                            ContentUnavailableView(courses.isEmpty ? "No courses yet" : "No courses match your search",
                                systemImage: "flag")
                        } else {
                            LazyVStack(spacing: 12) {
                                ForEach(filteredCourses) { course in
                                    NavigationLink(value: course) {
                                        HStack(spacing: 14) {
                                            Image(systemName: "flag.fill")
                                                .foregroundStyle(BirdieTheme.fairwayDark)
                                                .frame(width: 42, height: 42)
                                                .background(BirdieTheme.mint, in: RoundedRectangle(cornerRadius: 12))
                                            VStack(alignment: .leading, spacing: 4) {
                                                Text(course.name)
                                                    .font(BirdieTheme.body(17, weight: .semibold))
                                                    .foregroundStyle(BirdieTheme.ink)
                                                Text(course.location)
                                                    .font(BirdieTheme.body(13))
                                                    .foregroundStyle(BirdieTheme.muted)
                                                if course.isCustom {
                                                    Text("My course")
                                                        .font(BirdieTheme.mono(10))
                                                        .foregroundStyle(BirdieTheme.fairwayDark)
                                                }
                                            }
                                            Spacer(minLength: 0)
                                            Image(systemName: "chevron.right")
                                                .font(.caption.bold())
                                                .foregroundStyle(BirdieTheme.muted)
                                        }
                                        .birdieCard()
                                        .accessibilityElement(children: .combine)
                                    }
                                    .buttonStyle(.plain)
                                }
                            }
                        }
                    }
                    .padding(16)
                }
            }
            .refreshable { await loadCourses() }
            .background(BirdieTheme.canvas)
            .navigationDestination(for: CourseSummary.self) { course in CourseDetailView(course: course) }
            .birdieNavigationBrand()
            .toolbar {
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
