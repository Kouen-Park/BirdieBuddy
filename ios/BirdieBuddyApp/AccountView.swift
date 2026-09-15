import SwiftUI

struct AccountView: View {
    @EnvironmentObject private var appState: AppState
    @State private var displayName = ""
    @State private var isSaving = false
    @State private var message: String?
    @State private var password = ""
    @State private var isWorking = false

    var body: some View {
        NavigationStack {
            Form {
                Section("Profile") {
                    TextField("Display name", text: $displayName)
                    Text(appState.user?.email ?? "").foregroundStyle(.secondary)
                    Button(isSaving ? "Saving…" : "Save profile") {
                        Task {
                            isSaving = true
                            let saved = await appState.updateProfile(displayName: displayName.trimmingCharacters(in: .whitespacesAndNewlines))
                            message = saved ? "Profile saved" : appState.errorMessage
                            isSaving = false
                        }
                    }
                    .disabled(isSaving || displayName.trimmingCharacters(in: .whitespacesAndNewlines).count < 2)
                }
                if let message { Text(message).foregroundStyle(.secondary) }
                Section("Account tools") {
                    Button("Request password reset email") {
                        Task {
                            isWorking = true
                            do { try await appState.api.requestPasswordReset(); message = "If the account exists, a reset email has been sent." }
                            catch { message = AppState.message(for: error) }
                            isWorking = false
                        }
                    }
                    Button("Export my data") {
                        Task {
                            isWorking = true
                            do {
                                let data = try await appState.api.exportData()
                                let url = FileManager.default.temporaryDirectory.appendingPathComponent("birdie-buddy-export.json")
                                try data.write(to: url, options: .atomic)
                                message = "Export saved to \(url.lastPathComponent)."
                            } catch { message = AppState.message(for: error) }
                            isWorking = false
                        }
                    }
                    SecureField("Password to delete account", text: $password)
                    Button("Delete account", role: .destructive) {
                        Task {
                            isWorking = true
                            do { try await appState.api.deleteAccount(password: password); await appState.signOut() }
                            catch { message = AppState.message(for: error) }
                            isWorking = false
                        }
                    }
                    .disabled(isWorking || password.isEmpty)
                }
                Section {
                    Button("Sign out", role: .destructive) { Task { await appState.signOut() } }
                }
            }
            .navigationTitle("Account")
            .onAppear { displayName = appState.user?.displayName ?? "" }
        }
    }
}
