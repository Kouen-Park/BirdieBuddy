import Foundation
import SwiftUI

enum AppConfigurationError: Error, Equatable {
    case missingAPIBaseURL
    case invalidAPIBaseURL
    case insecureProductionAPIBaseURL
}

struct AppConfiguration {
    static var apiBaseURL: URL {
        let environmentValue = ProcessInfo.processInfo.environment["API_BASE_URL"]
        let bundledValue = Bundle.main.object(forInfoDictionaryKey: "APIBaseURL") as? String
        #if DEBUG
        let allowsInsecureLocalhost = true
        #else
        let allowsInsecureLocalhost = false
        #endif

        do {
            return try resolveAPIBaseURL(
                environmentValue: environmentValue,
                bundledValue: bundledValue,
                allowsInsecureLocalhost: allowsInsecureLocalhost
            )
        } catch {
            preconditionFailure("Invalid API base URL configuration: \(error)")
        }
    }

    static func resolveAPIBaseURL(
        environmentValue: String?,
        bundledValue: String?,
        allowsInsecureLocalhost: Bool
    ) throws -> URL {
        let rawValue = [environmentValue, bundledValue]
            .compactMap { $0?.trimmingCharacters(in: .whitespacesAndNewlines) }
            .first { !$0.isEmpty }
        guard let rawValue else { throw AppConfigurationError.missingAPIBaseURL }
        guard let url = URL(string: rawValue), let scheme = url.scheme?.lowercased(), let host = url.host?.lowercased() else {
            throw AppConfigurationError.invalidAPIBaseURL
        }

        let isLocalhost = host == "localhost" || host == "127.0.0.1" || host == "::1"
        if !allowsInsecureLocalhost && (scheme != "https" || isLocalhost) {
            throw AppConfigurationError.insecureProductionAPIBaseURL
        }
        guard scheme == "https" || (allowsInsecureLocalhost && scheme == "http" && isLocalhost) else {
            throw AppConfigurationError.invalidAPIBaseURL
        }
        return url
    }
}

@MainActor
final class AppState: ObservableObject {
    @Published private(set) var user: CurrentUser?
    @Published private(set) var isRestoring = true
    @Published var errorMessage: String?

    let api: APIClient
    private var roundDraftStores: [Int: RoundDraftStore] = [:]

    init(api: APIClient? = nil) {
        self.api = api ?? APIClient(baseURL: AppConfiguration.apiBaseURL)
    }

    var isSignedIn: Bool { user != nil }

    func roundDraftStore(for userId: Int) -> RoundDraftStore {
        if let store = roundDraftStores[userId] { return store }
        let store = RoundDraftStore(userId: userId)
        roundDraftStores[userId] = store
        return store
    }

    func restoreSession() async {
        await api.setSessionInvalidationHandler { [weak self] in
            await self?.handleSessionInvalidation()
        }
        user = await api.restoreSession()
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
        roundDraftStores.removeAll()
        errorMessage = nil
    }

    func updateProfile(displayName: String) async -> Bool {
        do { user = try await api.updateProfile(displayName: displayName); return true }
        catch { errorMessage = Self.message(for: error); return false }
    }

    private func loadCoursesSilently() async {
        _ = try? await api.courses()
    }

    private func handleSessionInvalidation() {
        user = nil
        roundDraftStores.removeAll()
        errorMessage = "Your session expired. Sign in again to sync saved changes."
    }

    static func message(for error: Error) -> String {
        if let problem = error as? ApiProblem { return problem.detail ?? problem.title ?? "Request failed." }
        if let urlError = error as? URLError { return urlError.localizedDescription }
        return error.localizedDescription
    }
}
