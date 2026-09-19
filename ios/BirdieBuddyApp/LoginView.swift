import SwiftUI

struct LoginView: View {
    @EnvironmentObject private var appState: AppState
    @State private var email = ""
    @State private var password = ""
    @State private var isSubmitting = false

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Birdie Buddy").font(.largeTitle.bold())
                        Text("Keep your round moving, even when the course connection does not.")
                            .foregroundStyle(.secondary)
                    }
                    .padding(.vertical, 12)
                }

                Section("Sign in") {
                    TextField("Email", text: $email)
                        .textContentType(.username)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .keyboardType(.emailAddress)
                        .accessibilityLabel("Email address")
                    SecureField("Password", text: $password)
                        .textContentType(.password)
                        .accessibilityLabel("Password")
                }

                if let errorMessage = appState.errorMessage {
                    Section {
                        Label(errorMessage, systemImage: "exclamationmark.triangle.fill")
                            .foregroundStyle(.red)
                    }
                }

                Section {
                    Button {
                        isSubmitting = true
                        Task {
                            await appState.signIn(email: email.trimmingCharacters(in: .whitespacesAndNewlines), password: password)
                            isSubmitting = false
                        }
                    } label: {
                        HStack {
                            Spacer()
                            if isSubmitting { ProgressView() } else { Text("Sign in").bold() }
                            Spacer()
                        }
                    }
                    .disabled(isSubmitting || email.isEmpty || password.isEmpty)
                    .accessibilityLabel("Sign in")
                }

                Section {
                    NavigationLink {
                        SignUpView()
                    } label: {
                        Text("New here? Create an account")
                    }
                    .disabled(isSubmitting)
                    .accessibilityLabel("Create an account")
                }
            }
            .navigationTitle("Welcome back")
        }
    }
}
