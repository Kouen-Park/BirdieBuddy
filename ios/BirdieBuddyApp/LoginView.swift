import SwiftUI

struct LoginView: View {
    @EnvironmentObject private var appState: AppState
    @State private var email = ""
    @State private var password = ""
    @State private var isSubmitting = false

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 21) {
                    HStack(spacing: 10) {
                        Image(systemName: "flag")
                            .foregroundStyle(BirdieTheme.sun)
                            .frame(width: 32, height: 32)
                            .overlay(RoundedRectangle(cornerRadius: 9).stroke(BirdieTheme.sun))
                        Text("Birdie Buddy")
                            .font(BirdieTheme.display(21))
                            .foregroundStyle(BirdieTheme.ink)
                    }
                    VStack(alignment: .leading, spacing: 9) {
                        Text("Welcome back")
                            .font(BirdieTheme.display(36))
                            .foregroundStyle(BirdieTheme.ink)
                        Text("Keep your round moving, even when the course connection does not.")
                            .font(BirdieTheme.body(15))
                            .foregroundStyle(BirdieTheme.muted)
                    }

                    VStack(spacing: 12) {
                        TextField("Email", text: $email)
                            .textContentType(.username)
                            .textInputAutocapitalization(.never)
                            .autocorrectionDisabled()
                            .keyboardType(.emailAddress)
                            .accessibilityLabel("Email address")
                            .authField()
                        SecureField("Password", text: $password)
                            .textContentType(.password)
                            .accessibilityLabel("Password")
                            .authField()
                    }

                    if let errorMessage = appState.errorMessage {
                        Label(errorMessage, systemImage: "exclamationmark.triangle.fill")
                            .font(BirdieTheme.body(13))
                            .foregroundStyle(BirdieTheme.flag)
                    }
                    Button {
                        isSubmitting = true
                        Task {
                            await appState.signIn(email: email.trimmingCharacters(in: .whitespacesAndNewlines), password: password)
                            isSubmitting = false
                        }
                    } label: {
                        HStack(spacing: 8) {
                            if isSubmitting { ProgressView() } else { Text("Sign in").bold() }
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .disabled(isSubmitting || email.isEmpty || password.isEmpty)
                    .accessibilityLabel("Sign in")
                    .buttonStyle(.borderedProminent)
                    .controlSize(.large)
                    .tint(BirdieTheme.fairwayDark)

                    NavigationLink {
                        SignUpView()
                    } label: {
                        Text("New here? Create an account")
                            .font(BirdieTheme.body(14, weight: .semibold))
                            .frame(maxWidth: .infinity)
                    }
                    .disabled(isSubmitting)
                    .accessibilityLabel("Create an account")
                }
                .birdieCard(padding: 24)
                .frame(maxWidth: 520)
                .padding(20)
                .frame(maxWidth: .infinity)
            }
            .background(BirdieTheme.canvas)
            .toolbar(.hidden, for: .navigationBar)
        }
    }
}

private extension View {
    func authField() -> some View {
        self
            .font(BirdieTheme.body(16))
            .padding(13)
            .background(BirdieTheme.canvas, in: RoundedRectangle(cornerRadius: 10))
            .overlay(RoundedRectangle(cornerRadius: 10).stroke(BirdieTheme.line))
    }
}
