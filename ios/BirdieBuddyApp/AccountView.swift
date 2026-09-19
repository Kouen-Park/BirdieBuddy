import SwiftUI

struct AccountView: View {
    @EnvironmentObject private var appState: AppState
    @ObservedObject private var diagnostics = CrashDiagnosticsReporter.shared
    @State private var displayName = ""
    @State private var isSaving = false
    @State private var message: String?
    @State private var password = ""
    @State private var isWorking = false
    @State private var currentPassword = ""
    @State private var newPassword = ""
    @State private var isChangingPassword = false

    /// Mirrors the server's ChangePasswordDto rule so a doomed request is not sent.
    private var newPasswordIsValid: Bool {
        newPassword.count >= 8 && newPassword.count <= 128
            && newPassword.contains(where: \.isLetter)
            && newPassword.contains(where: \.isNumber)
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Profile") {
                    TextField("Display name", text: $displayName)
                        .accessibilityLabel("Display name")
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
                Section("Email") {
                    if appState.user?.emailVerified == true {
                        Label("Email verified", systemImage: "checkmark.seal")
                            .foregroundStyle(.green)
                    } else {
                        Label("Email not verified", systemImage: "exclamationmark.triangle")
                            .foregroundStyle(.orange)
                        Button(isWorking ? "Sending…" : "Send verification email") {
                            Task {
                                isWorking = true
                                message = await appState.sendVerificationEmail()
                                    ? "Verification email sent. Open the link on this device to finish."
                                    : appState.errorMessage
                                isWorking = false
                            }
                        }
                        .disabled(isWorking)
                        .accessibilityLabel("Send verification email")
                    }
                }

                Section("Change password") {
                    SecureField("Current password", text: $currentPassword)
                        .textContentType(.password)
                        .accessibilityLabel("Current password")
                    SecureField("New password", text: $newPassword)
                        .textContentType(.newPassword)
                        .accessibilityLabel("New password")
                        .accessibilityHint("At least 8 characters, including a letter and a number")
                    Text("Use at least 8 characters with one letter and one number.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                    Button(isChangingPassword ? "Changing…" : "Change password") {
                        Task {
                            isChangingPassword = true
                            // A success signs the golfer out, so this view disappears;
                            // only the failure branch needs to update local state.
                            if await appState.changePassword(
                                currentPassword: currentPassword, newPassword: newPassword) {
                                currentPassword = ""
                                newPassword = ""
                            } else {
                                message = appState.errorMessage
                            }
                            isChangingPassword = false
                        }
                    }
                    .disabled(isChangingPassword || currentPassword.isEmpty || !newPasswordIsValid)
                    .accessibilityLabel("Change password")
                }

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
                            do { try await appState.deleteAccount(password: password) }
                            catch { message = AppState.message(for: error) }
                            isWorking = false
                        }
                    }
                    .disabled(isWorking || password.isEmpty)
                }
                Section {
                    Button("Sign out", role: .destructive) { Task { await appState.signOut() } }
                        .accessibilityLabel("Sign out")
                }

                Section("Diagnostics") {
                    if diagnostics.reports.isEmpty {
                        Text("No crash or hang reports on this device.")
                            .font(.footnote)
                            .foregroundStyle(.secondary)
                    } else {
                        ForEach(diagnostics.reports) { report in
                            ShareLink(item: report.url) {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text(report.capturedAt.formatted(date: .abbreviated, time: .shortened))
                                    Text("Share report").font(.caption).foregroundStyle(.secondary)
                                }
                            }
                            .accessibilityLabel("Share diagnostic report from \(report.capturedAt.formatted(date: .abbreviated, time: .shortened))")
                        }
                        Button("Delete reports", role: .destructive) { diagnostics.deleteAll() }
                            .accessibilityLabel("Delete diagnostic reports")
                    }
                }
            }
            .navigationTitle("Account")
            .onAppear {
                displayName = appState.user?.displayName ?? ""
                diagnostics.reloadReports()
            }
        }
    }
}
