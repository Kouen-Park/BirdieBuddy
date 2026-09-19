import SwiftUI

/// Completes a password reset from a token that arrived by email.
///
/// Reachable while signed out, because that is when a reset happens. The token
/// is the credential here, so no session is required.
struct PasswordResetView: View {
    let token: String

    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss

    @State private var newPassword = ""
    @State private var isSubmitting = false
    @State private var errorMessage: String?
    @State private var didReset = false

    private var passwordIsValid: Bool {
        newPassword.count >= 8 && newPassword.count <= 128
            && newPassword.contains(where: \.isLetter)
            && newPassword.contains(where: \.isNumber)
    }

    var body: some View {
        NavigationStack {
            Form {
                if didReset {
                    Section {
                        Label("Password updated. Sign in with your new password.",
                              systemImage: "checkmark.seal")
                            .foregroundStyle(.green)
                    }
                } else {
                    Section("New password") {
                        SecureField("New password", text: $newPassword)
                            .textContentType(.newPassword)
                            .accessibilityLabel("New password")
                            .accessibilityHint("At least 8 characters, including a letter and a number")
                        Text("Use at least 8 characters with one letter and one number.")
                            .font(.footnote)
                            .foregroundStyle(.secondary)
                    }

                    if let errorMessage {
                        Section {
                            Label(errorMessage, systemImage: "exclamationmark.triangle.fill")
                                .foregroundStyle(.red)
                        }
                    }

                    Section {
                        Button(isSubmitting ? "Updating…" : "Set new password") {
                            Task { await submit() }
                        }
                        .disabled(isSubmitting || !passwordIsValid)
                        .accessibilityLabel("Set new password")
                    }
                }
            }
            .navigationTitle("Reset password")
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Done") { dismiss() }.disabled(isSubmitting)
                }
            }
        }
    }

    private func submit() async {
        isSubmitting = true
        errorMessage = nil
        do {
            try await appState.api.resetPassword(token: token, newPassword: newPassword)
            newPassword = ""
            didReset = true
        } catch {
            errorMessage = AppState.message(for: error)
        }
        isSubmitting = false
    }
}
