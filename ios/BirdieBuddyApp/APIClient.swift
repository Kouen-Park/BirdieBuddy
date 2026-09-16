import Foundation

actor APIClient {
    let baseURL: URL
    private let keychain: any SessionStoring
    private let urlSession: URLSession
    private var session: MobileSession?
    private var sessionInvalidated: (@Sendable () async -> Void)?

    init(baseURL: URL, keychain: any SessionStoring = KeychainStore(), urlSession: URLSession = .shared) {
        self.baseURL = baseURL
        self.keychain = keychain
        self.urlSession = urlSession
        if let stored = try? keychain.load() {
            self.session = stored
        } else {
            self.session = nil
            keychain.remove()
        }
    }

    func currentUser() -> CurrentUser? { session?.user }

    func setSessionInvalidationHandler(_ handler: @escaping @Sendable () async -> Void) {
        sessionInvalidated = handler
    }

    func restoreSession() async -> CurrentUser? {
        guard let cached = session else { return nil }
        do {
            try await refresh()
            return session?.user
        } catch is URLError {
            return cached.user
        } catch let problem as ApiProblem where problem.status == 401 || problem.status == 403 {
            await invalidateSession()
            return nil
        } catch {
            // A transient server failure must not erase a still-usable offline session.
            return cached.user
        }
    }

    func signIn(email: String, password: String) async throws -> CurrentUser {
        let result: MobileSession = try await send(path: "/api/mobile/auth/session", method: "POST", body: MobileSessionRequest(email: email, password: password), authenticated: false)
        try keychain.save(result)
        session = result
        return result.user
    }

    func signOut() async {
        _ = try? await sendEmpty(path: "/api/mobile/auth/revoke", method: "POST")
        await invalidateSession(notify: false)
    }

    func courses() async throws -> [CourseSummary] {
        try await send(path: "/api/courses", method: "GET", body: Optional<EmptyBody>.none, authenticated: true)
    }

    func course(id: Int) async throws -> CourseDetail {
        try await send(path: "/api/courses/\(id)", method: "GET", body: Optional<EmptyBody>.none, authenticated: false)
    }

    func startDraft(courseId: Int, teeId: Int?, date: Date = .now) async throws -> RoundDraft {
        let formatter = DateFormatter(); formatter.calendar = Calendar(identifier: .gregorian); formatter.dateFormat = "yyyy-MM-dd"
        return try await send(path: "/api/rounds/drafts", method: "POST", body: RoundStartRequest(courseId: courseId, date: formatter.string(from: date), courseTeeId: teeId, tee: nil), authenticated: true)
    }

    func round(id: Int) async throws -> RoundDetail {
        try await send(path: "/api/rounds/\(id)", method: "GET", body: Optional<EmptyBody>.none, authenticated: true)
    }

    func saveHole(roundId: Int, holeNumber: Int, request: HoleUpsertRequest) async throws -> Hole {
        try await send(path: "/api/rounds/\(roundId)/holes/by-number/\(holeNumber)", method: "PUT", body: request, authenticated: true)
    }

    func complete(roundId: Int) async throws -> RoundDraft {
        try await send(path: "/api/rounds/\(roundId)/complete", method: "POST", body: Optional<EmptyBody>.none, authenticated: true)
    }

    func rounds() async throws -> [RoundSummary] {
        try await send(path: "/api/rounds", method: "GET", body: Optional<EmptyBody>.none, authenticated: true)
    }

    func statistics() async throws -> OverviewStatistics {
        try await send(path: "/api/statistics/overview", method: "GET", body: Optional<EmptyBody>.none, authenticated: true)
    }

    func updateProfile(displayName: String) async throws -> CurrentUser {
        try await send(path: "/api/auth/profile", method: "PUT", body: UpdateProfileRequest(displayName: displayName), authenticated: true)
    }

    func practiceSessions() async throws -> [PracticeSession] {
        try await send(path: "/api/practice/sessions", method: "GET", body: Optional<EmptyBody>.none, authenticated: true)
    }

    func startPractice(focusCode: String, drillTitle: String, minutes: Int) async throws -> PracticeSession {
        try await send(path: "/api/practice/sessions", method: "POST", body: PracticeSessionRequest(focusCode: focusCode, drillTitle: drillTitle, minutes: minutes), authenticated: true)
    }

    func completePractice(id: Int, result: String?, notes: String?) async throws -> PracticeSession {
        try await send(path: "/api/practice/sessions/\(id)/complete", method: "POST", body: PracticeCompletionRequest(result: result, notes: notes), authenticated: true)
    }

    func requestPasswordReset() async throws {
        try await sendEmpty(path: "/api/auth/forgot-password", method: "POST", body: PasswordResetRequest(email: session?.user.email ?? ""), authenticated: false)
    }

    func exportData() async throws -> Data {
        let (data, response) = try await perform(path: "/api/auth/export", method: "GET", body: Optional<EmptyBody>.none, authenticated: true)
        guard (200..<300).contains(response.statusCode) else { throw try decodeProblem(data) }
        return data
    }

    func deleteAccount(password: String) async throws {
        try await sendEmpty(path: "/api/auth/delete-account", method: "POST",
            body: DeleteAccountRequest(password: password, confirmation: "DELETE MY ACCOUNT"), authenticated: true)
        await invalidateSession()
    }

    private func send<T: Decodable, Body: Encodable>(path: String, method: String, body: Body?, authenticated: Bool) async throws -> T {
        let (data, response) = try await perform(path: path, method: method, body: body, authenticated: authenticated)
        guard (200..<300).contains(response.statusCode) else { throw try decodeProblem(data) }
        return try JSONDecoder.birdieBuddy.decode(T.self, from: data)
    }

    private func sendEmpty(path: String, method: String) async throws {
        try await sendEmpty(path: path, method: method, body: Optional<EmptyBody>.none, authenticated: true)
    }

    private func sendEmpty<Body: Encodable>(path: String, method: String, body: Body?, authenticated: Bool) async throws {
        let (data, response) = try await perform(path: path, method: method, body: body, authenticated: authenticated)
        guard (200..<300).contains(response.statusCode) else { throw try decodeProblem(data) }
    }

    private func perform<Body: Encodable>(
        path: String,
        method: String,
        body: Body?,
        authenticated: Bool,
        canRefresh: Bool = true
    ) async throws -> (Data, HTTPURLResponse) {
        var request = try makeRequest(path: path, method: method, authenticated: authenticated)
        if let body { request.httpBody = try JSONEncoder.birdieBuddy.encode(body); request.setValue("application/json", forHTTPHeaderField: "Content-Type") }
        let (data, response) = try await urlSession.data(for: request)
        guard let http = response as? HTTPURLResponse else { throw URLError(.badServerResponse) }

        if http.statusCode == 401 && authenticated {
            guard canRefresh else {
                let problem = try decodeProblem(data)
                await invalidateSession()
                throw problem
            }
            do {
                try await refresh()
            } catch {
                if let problem = error as? ApiProblem, problem.status == 401 || problem.status == 403 {
                    await invalidateSession()
                }
                throw error
            }
            return try await perform(path: path, method: method, body: body, authenticated: true, canRefresh: false)
        }
        return (data, http)
    }

    private func refresh() async throws {
        guard let current = session else { throw ApiProblem(status: 401, title: "Sign in required.", detail: nil, code: "auth.refresh_required", traceId: nil) }
        let refreshed: MobileSession = try await send(path: "/api/mobile/auth/refresh", method: "POST", body: MobileRefreshRequest(refreshToken: current.refreshToken), authenticated: false)
        try keychain.save(refreshed)
        session = refreshed
    }

    private func invalidateSession(notify: Bool = true) async {
        let hadSession = session != nil
        keychain.remove()
        session = nil
        if notify, hadSession { await sessionInvalidated?() }
    }

    private func makeRequest(path: String, method: String, authenticated: Bool) throws -> URLRequest {
        guard let url = URL(string: path, relativeTo: baseURL) else { throw URLError(.badURL) }
        var request = URLRequest(url: url); request.httpMethod = method; request.timeoutInterval = 20
        if authenticated, let token = session?.accessToken { request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization") }
        return request
    }

    private func decodeProblem(_ data: Data) throws -> ApiProblem {
        (try? JSONDecoder.birdieBuddy.decode(ApiProblem.self, from: data)) ?? ApiProblem(status: nil, title: "Request failed.", detail: nil, code: nil, traceId: nil)
    }
}

private struct EmptyBody: Encodable {}
