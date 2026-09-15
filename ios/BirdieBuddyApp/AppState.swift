import Foundation
import SwiftUI

@MainActor
final class AppState: ObservableObject {
    @Published private(set) var user: CurrentUser?
    @Published private(set) var isRestoring = true
    @Published var errorMessage: String?

    let api: APIClient

    init() {
        let configured = ProcessInfo.processInfo.environment["API_BASE_URL"] ?? "http://localhost:5000"
        api = APIClient(baseURL: URL(string: configured)!)
    }

    var isSignedIn: Bool { user != nil }

    func restoreSession() async {
        user = await api.currentUser()
        isRestoring = false
        if user != nil { await loadCoursesSilently() }
    }

    func signIn(email: String, password: String) async {
        errorMessage = nil
        do { user = try await api.signIn(email: email, password: password) }
        catch { errorMessage = Self.message(for: error) }
    }

    func signOut() async {
        await api.signOut()
        user = nil
        errorMessage = nil
    }

    func updateProfile(displayName: String) async -> Bool {
        do { user = try await api.updateProfile(displayName: displayName); return true }
        catch { errorMessage = Self.message(for: error); return false }
    }

    private func loadCoursesSilently() async {
        _ = try? await api.courses()
    }

    static func message(for error: Error) -> String {
        if let problem = error as? ApiProblem { return problem.detail ?? problem.title ?? "Request failed." }
        if let urlError = error as? URLError { return urlError.localizedDescription }
        return error.localizedDescription
    }
}
