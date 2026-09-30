// Manual visual-review harness. Append to ScorecardLayoutTests.swift temporarily
// to capture the real SwiftUI views with isolated fixture data on a simulator.
@MainActor
final class DesignReviewCaptureTests: XCTestCase {
    func testCaptureScreens() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [DesignReviewProtocol.self]
        let api = APIClient(baseURL: URL(string: "https://design.example.test")!,
            keychain: DesignReviewSessionStore(), urlSession: URLSession(configuration: configuration))
        let appState = AppState(api: api, roundPersistence: try RoundPersistenceStore(inMemory: true))
        await appState.restoreSession()
        let round = RoundSummary(id: 10, courseId: 3, courseName: "Waitemata Golf Club",
            date: "2026-09-30", tee: "Blue", totalScore: 84, scoreToPar: 12,
            status: "Completed", holesPlayed: 18, expectedHoles: 18)
        let draft = try JSONDecoder.birdieBuddy.decode(RoundDraft.self, from: Data(DesignReviewProtocol.draft.utf8))
        let course = CourseSummary(id: 3, name: "Waitemata Golf Club", location: "Auckland, New Zealand", custom: false)
        let screens: [(String, AnyView)] = [
            ("login", AnyView(LoginView())),
            ("courses", AnyView(CourseBrowserView())),
            ("rounds", AnyView(RoundHistoryView())),
            ("statistics", AnyView(StatisticsView())),
            ("practice", AnyView(PracticeView())),
            ("account", AnyView(AccountView())),
            ("course-detail", AnyView(NavigationStack { CourseDetailView(course: course) })),
            ("round-detail", AnyView(NavigationStack { RoundDetailView(round: round) })),
            ("live-round", AnyView(NavigationStack { LiveRoundView(draft: draft) })),
            ("signup", AnyView(NavigationStack { SignUpView() })),
            ("password-reset", AnyView(PasswordResetView(token: "design-fixture"))),
            ("course-form", AnyView(CourseFormView(mode: .create, onSaved: {})))
        ]
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.first as? UIWindowScene)
        let previousWindow = scene.windows.first(where: \.isKeyWindow)
        let window = UIWindow(windowScene: scene)
        window.frame = CGRect(x: 0, y: 0, width: 390, height: 844)
        window.overrideUserInterfaceStyle = .light
        defer { window.isHidden = true; previousWindow?.makeKeyAndVisible() }
        for (name, screen) in screens {
            for locale in ["en", "ko"] {
                try await capture(name + "-" + locale, view: screen, state: appState,
                    typeSize: .large, locale: locale, window: window)
            }
        }
        for (name, screen) in screens where ["courses", "rounds", "statistics", "live-round", "account"].contains(name) {
            try await capture(name + "-ko-accessibility", view: screen, state: appState,
                typeSize: .accessibility5, locale: "ko", window: window)
        }
    }

    private func capture(_ name: String, view: AnyView, state: AppState,
                         typeSize: DynamicTypeSize, locale: String, window: UIWindow) async throws {
        let content = view.environmentObject(state)
            .environment(\.dynamicTypeSize, typeSize)
            .environment(\.locale, Locale(identifier: locale))
            .font(BirdieTheme.body()).tint(BirdieTheme.fairwayDark).preferredColorScheme(.light)
        let controller = UIHostingController(rootView: content)
        window.rootViewController = controller
        window.makeKeyAndVisible()
        try await Task.sleep(nanoseconds: 600_000_000)
        controller.view.layoutIfNeeded()
        let renderer = UIGraphicsImageRenderer(bounds: window.bounds)
        let snapshot = renderer.image { _ in window.drawHierarchy(in: window.bounds, afterScreenUpdates: true) }
        let attachment = XCTAttachment(image: snapshot)
        attachment.name = name
        attachment.lifetime = .keepAlways
        add(attachment)
        XCTAssertGreaterThan(snapshot.size.width, 0)
        window.rootViewController = nil
    }
}

private final class DesignReviewSessionStore: SessionStoring {
    var session: MobileSession? = MobileSession(accessToken: "design-fixture", refreshToken: "design-fixture",
        accessTokenExpiresAt: .distantFuture, refreshTokenExpiresAt: .distantFuture,
        user: CurrentUser(id: 7, email: "golfer@example.test", displayName: "Alex Park", emailVerified: true))
    func save(_ session: MobileSession) throws { self.session = session }
    func load() throws -> MobileSession? { session }
    func remove() { session = nil }
}

private final class DesignReviewProtocol: URLProtocol {
    static let summary = #"{"id":10,"courseId":3,"courseName":"Waitemata Golf Club","date":"2026-09-30","tee":"Blue","totalScore":84,"scoreToPar":12,"status":"Completed","holesPlayed":18,"expectedHoles":18}"#
    static let draft = #"{"id":10,"courseId":3,"courseName":"Waitemata Golf Club","date":"2026-09-30","courseTeeId":4,"tee":"Blue","holes":[],"status":"Draft","currentHole":1,"expectedHoles":18,"updatedAt":null}"#
    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func stopLoading() {}
    override func startLoading() {
        let path = request.url!.path
        let body: String
        switch path {
        case "/api/auth/me":
            body = #"{"id":7,"email":"golfer@example.test","displayName":"Alex Park","emailVerified":true}"#
        case "/api/courses":
            body = #"[{"id":3,"name":"Waitemata Golf Club","location":"Auckland, New Zealand","custom":false},{"id":4,"name":"Royal Auckland and Grange Golf Club","location":"Auckland, New Zealand","custom":false},{"id":5,"name":"My weekend course","location":"North Shore","custom":true}]"#
        case "/api/courses/3":
            body = #"{"id":3,"name":"Waitemata Golf Club","location":"Auckland, New Zealand","tees":[{"id":4,"name":"Blue","nineHoles":false,"totalPar":72,"holes":[]}],"holes":[],"custom":false}"#
        case "/api/rounds/page":
            body = request.url!.query?.contains("status=Draft") == true
                ? #"{"items":[],"nextCursor":null}"#
                : "{\"items\":[\(Self.summary)],\"nextCursor\":null}"
        case "/api/rounds/10": body = Self.draft
        case "/api/statistics/overview":
            body = "{\"roundsPlayed\":12,\"averageScore\":86.4,\"bestScore\":79,\"averagePutts\":32.1,\"averageGirPercentage\":38.2,\"averageFairwayPercentage\":54.8,\"recentRounds\":[\(Self.summary)],\"averagePuttsPerHole\":1.78}"
        case "/api/statistics/round/10":
            body = #"{"roundId":10,"totalScore":84,"scoreToPar":12,"totalPutts":32,"averagePuttsPerHole":1.78,"girPercentage":38.2,"fairwayPercentage":54.8,"totalPenalties":1,"eagles":0,"birdies":2,"pars":5,"bogeys":8,"doubleBogeysOrWorse":3,"par3":{"holesPlayed":4,"averageScore":3.5,"averageScoreToPar":0.5},"par4":{"holesPlayed":10,"averageScore":4.7,"averageScoreToPar":0.7},"par5":{"holesPlayed":4,"averageScore":5.75,"averageScoreToPar":0.75}}"#
        case "/api/practice/sessions":
            body = #"[{"id":1,"focusCode":"Putting","drillTitle":"Distance control","minutes":30,"startedAt":"2026-09-30T01:00:00Z","completedAt":null,"result":null,"notes":null},{"id":2,"focusCode":"Short game","drillTitle":"Greenside bunker shots","minutes":20,"startedAt":"2026-09-29T01:00:00Z","completedAt":"2026-09-29T01:20:00Z","result":null,"notes":null}]"#
        default: body = "{}"
        }
        let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil,
            headerFields: ["Content-Type": "application/json"])!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: Data(body.utf8))
        client?.urlProtocolDidFinishLoading(self)
    }
}
