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
    @Environment(\.scenePhase) private var scenePhase
    @StateObject private var connectivity = ConnectivityMonitor()

    var body: some View {
        Group {
            if appState.isRestoring {
                VStack(spacing: 12) {
                    ProgressView()
                    Text("Restoring your session…")
                        .foregroundStyle(.secondary)
                }
            } else if appState.isSignedIn {
                MainTabView()
            } else {
                LoginView()
            }
        }
        .task { await appState.restoreSession() }
        .onChange(of: connectivity.isOnline) { _, isOnline in
            if isOnline { Task { await appState.syncPending(trigger: .networkRestored) } }
        }
        .onChange(of: scenePhase) { _, phase in
            if phase == .active { Task { await appState.syncPending(trigger: .foreground) } }
        }
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
