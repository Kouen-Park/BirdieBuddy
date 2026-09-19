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
    /// `sheet(item:)` needs an Identifiable value; wrapping the token avoids a
    /// retroactive Identifiable conformance on String.
    private struct ResetLink: Identifiable {
        let id = UUID()
        let token: String
    }

    @EnvironmentObject private var appState: AppState
    @Environment(\.scenePhase) private var scenePhase
    @StateObject private var connectivity = ConnectivityMonitor()
    @State private var resetLink: ResetLink?
    @State private var linkMessage = ""
    @State private var isShowingLinkMessage = false

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
        .task {
            CrashDiagnosticsReporter.shared.start()
            await appState.restoreSession()
        }
        .onChange(of: connectivity.isOnline) { _, isOnline in
            if isOnline { Task { await appState.syncPending(trigger: .networkRestored) } }
        }
        .onChange(of: scenePhase) { _, phase in
            if phase == .active { Task { await appState.syncPending(trigger: .foreground) } }
        }
        .onOpenURL { url in handle(url) }
        .sheet(item: $resetLink) { link in
            PasswordResetView(token: link.token)
        }
        .alert("Email verification", isPresented: $isShowingLinkMessage) {
            Button("OK") {}
        } message: {
            Text(linkMessage)
        }
    }

    /// Accepts `birdiebuddy://verify-email?token=…` and
    /// `birdiebuddy://reset-password?token=…`. Universal Links would additionally
    /// need an apple-app-site-association file served from the API domain, so the
    /// custom scheme is what works before a domain is wired up.
    private func handle(_ url: URL) {
        guard let components = URLComponents(url: url, resolvingAgainstBaseURL: false) else { return }
        let action = components.host ?? components.path.split(separator: "/").first.map(String.init) ?? ""
        guard let token = components.queryItems?.first(where: { $0.name == "token" })?.value,
              !token.isEmpty else { return }

        switch action {
        case "verify-email":
            Task {
                linkMessage = await appState.verifyEmail(token: token)
                    ? "Your email is verified."
                    : (appState.errorMessage ?? "That verification link is no longer valid.")
                isShowingLinkMessage = true
            }
        case "reset-password":
            resetLink = ResetLink(token: token)
        default:
            break
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
