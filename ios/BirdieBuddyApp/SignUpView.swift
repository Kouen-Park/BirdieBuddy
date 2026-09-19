import SwiftUI

/// Account creation for the native client. The rules mirror the server's `RegisterDto`
/// validation so a golfer is told what is wrong before a request is spent.
struct SignUpView: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss

    @State private var email = ""
    @State private var displayName = ""
    @State private var password = ""
    @State private var isSubmitting = false
    @State private var verificationEmail: String?

    private var trimmedEmail: String { email.trimmingCharacters(in: .whitespacesAndNewlines) }
    private var trimmedDisplayName: String { displayName.trimmingCharacters(in: .whitespacesAndNewlines) }

    private var emailLooksValid: Bool {
        let parts = trimmedEmail.split(separator: "@")
        return parts.count == 2 && !parts[0].isEmpty && parts[1].contains(".") && trimmedEmail.count <= 320
    }

    private var displayNameIsValid: Bool {
        (2...80).contains(trimmedDisplayName.count)
    }

    private var passwordIsValid: Bool {
        password.count >= 8 && password.count <= 128
            && password.contains(where: \.isLetter)
            && password.contains(where: \.isNumber)
    }

    private var canSubmit: Bool {
        !isSubmitting && emailLooksValid && displayNameIsValid && passwordIsValid
    }

    var body: some View {
        Form {
            Section("Your details") {
                TextField("Email", text: $email)
                    .textContentType(.username)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
                    .keyboardType(.emailAddress)
                    .accessibilityLabel("Email address")
                TextField("Display name", text: $displayName)
                    .textContentType(.name)
                    .accessibilityLabel("Display name")
                SecureField("Password", text: $password)
                    .textContentType(.newPassword)
                    .accessibilityLabel("Password")
                    .accessibilityHint("At least 8 characters, including a letter and a number")
            }

            Section {
                Text("Use at least 8 characters with one letter and one number.")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }

            if let errorMessage = appState.errorMessage {
                Section {
                    Label(errorMessage, systemImage: "exclamationmark.triangle.fill")
                        .foregroundStyle(.red)
                }
            }

            if let verificationEmail {
                Section {
                    Label("Check \(verificationEmail) for a verification link, then sign in.",
                          systemImage: "envelope.badge")
                        .foregroundStyle(.secondary)
                }
            }

            Section {
                Button {
                    Task { await submit() }
                } label: {
                    HStack {
                        Spacer()
                        if isSubmitting { ProgressView() } else { Text("Create account").bold() }
                        Spacer()
                    }
                }
                .disabled(!canSubmit)
                .accessibilityLabel("Create account")
            }
        }
        .navigationTitle("Create account")
        .interactiveDismissDisabled(isSubmitting)
    }

    private func submit() async {
        isSubmitting = true
        verificationEmail = nil
        let outcome = await appState.signUp(
            email: trimmedEmail,
            displayName: trimmedDisplayName,
            password: password)
        isSubmitting = false

        switch outcome {
        case .signedIn:
            // RootView swaps to the signed-in tabs on its own.
            password = ""
        case .verificationRequired(let address):
            password = ""
            verificationEmail = address
        case nil:
            break
        }
    }
}
