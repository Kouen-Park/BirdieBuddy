import SwiftUI

@main
struct BirdieBuddyApp: App {
    @StateObject private var appState = AppState()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environmentObject(appState)
        }
    }
}

struct RootView: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        Group {
            if appState.isSignedIn {
                MainTabView()
            } else {
                LoginView()
            }
        }
        .task { await appState.restoreSession() }
    }
}

struct MainTabView: View {
    var body: some View {
        TabView {
            CourseBrowserView()
                .tabItem { Label("Courses", systemImage: "flag") }
            RoundHistoryView()
                .tabItem { Label("Rounds", systemImage: "list.bullet.rectangle") }
            StatisticsView()
                .tabItem { Label("Stats", systemImage: "chart.bar.xaxis") }
            PracticeView()
                .tabItem { Label("Practice", systemImage: "figure.golf") }
            AccountView()
                .tabItem { Label("Account", systemImage: "person.crop.circle") }
        }
    }
}
