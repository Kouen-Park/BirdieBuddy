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
    @Published private(set) var roundSyncStatuses: [Int: RoundSyncStatus] = [:]

    let api: APIClient
    let roundPersistence: RoundPersistenceStore
    private let syncCoordinator: SyncCoordinator

    init(api: APIClient? = nil, roundPersistence: RoundPersistenceStore? = nil) {
        let resolvedAPI = api ?? APIClient(baseURL: AppConfiguration.apiBaseURL)
        let resolvedPersistence: RoundPersistenceStore
        if let roundPersistence {
            resolvedPersistence = roundPersistence
        } else {
            do { resolvedPersistence = try RoundPersistenceStore() }
            catch { preconditionFailure("Could not open round persistence: \(error)") }
        }
        self.api = resolvedAPI
        self.roundPersistence = resolvedPersistence
        syncCoordinator = SyncCoordinator(persistence: resolvedPersistence, api: resolvedAPI)
    }

    var isSignedIn: Bool { user != nil }

    func restoreSession() async {
        await api.setSessionInvalidationHandler { [weak self] in
            await self?.handleSessionInvalidation()
        }
        user = await api.restoreSession()
        isRestoring = false
        if user != nil {
            await loadCoursesSilently()
            _ = await syncPending(trigger: .sessionRestore)
        }
    }

    func signIn(email: String, password: String) async {
        errorMessage = nil
        do {
            user = try await api.signIn(email: email, password: password)
            _ = await syncPending(trigger: .reauthentication)
        }
        catch { errorMessage = Self.message(for: error) }
    }

    func signOut() async {
        await api.signOut()
        user = nil
        roundSyncStatuses = [:]
        errorMessage = nil
    }

    func deleteAccount(password: String) async throws {
        guard let userId = user?.id else { return }
        try await api.deleteAccount(password: password)
        try await roundPersistence.deleteUserData(userId: userId)
        user = nil
        roundSyncStatuses = [:]
        errorMessage = nil
    }

    @discardableResult
    func syncPending(trigger: SyncTrigger, roundId: Int? = nil) async -> SyncReport {
        guard let userId = user?.id else { return SyncReport() }
        let report = await syncCoordinator.synchronize(userId: userId, trigger: trigger, roundId: roundId)
        roundSyncStatuses = (try? await roundPersistence.statuses(userId: userId)) ?? [:]
        return report
    }

    func refreshSyncStatuses() async {
        guard let userId = user?.id else { roundSyncStatuses = [:]; return }
        roundSyncStatuses = (try? await roundPersistence.statuses(userId: userId)) ?? [:]
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
        roundSyncStatuses = [:]
        errorMessage = "Your session expired. Sign in again to sync saved changes."
    }

    static func message(for error: Error) -> String {
        if let problem = error as? ApiProblem { return problem.detail ?? problem.title ?? "Request failed." }
        if let urlError = error as? URLError { return urlError.localizedDescription }
        return error.localizedDescription
    }
}
