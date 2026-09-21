import Foundation

struct CurrentUser: Codable, Equatable, Identifiable {
    let id: Int
    let email: String
    let displayName: String
    let emailVerified: Bool
}

struct MobileSession: Codable, Equatable {
    let accessToken: String
    let refreshToken: String
    let accessTokenExpiresAt: Date
    let refreshTokenExpiresAt: Date
    let user: CurrentUser
}

struct MobileSessionRequest: Encodable {
    let email: String
    let password: String
}

struct MobileRefreshRequest: Encodable {
    let refreshToken: String
}

struct MobileRegistrationRequest: Encodable {
    let email: String
    let displayName: String
    let password: String
}

/// `POST /api/mobile/auth/register` answers `200` with a token session, or `202` with
/// this body when the deployment requires a verified email before first sign-in.
struct PendingVerification: Decodable, Equatable {
    let requiresEmailVerification: Bool
    let email: String
}

enum RegistrationOutcome: Equatable {
    case signedIn(CurrentUser)
    case verificationRequired(email: String)
}

struct ApiProblem: Decodable, Error {
    let status: Int?
    let title: String?
    let detail: String?
    let code: String?
    let traceId: String?
}

struct CourseSummary: Codable, Identifiable, Equatable, Hashable {
    let id: Int
    let name: String
    let location: String
    /// True only for a course this user created. Shared imported courses cannot
    /// be edited or deleted, so the UI must not offer those actions for them.
    let custom: Bool?

    var isCustom: Bool { custom ?? false }
}

struct CourseDetail: Codable, Identifiable, Equatable {
    let id: Int
    let name: String
    let location: String
    let tees: [CourseTee]
    let holes: [CourseHole]
    let custom: Bool?

    var isCustom: Bool { custom ?? false }
}

struct CourseTee: Codable, Identifiable, Equatable {
    let id: Int
    let name: String
    let nineHoles: Bool
    let totalPar: Int?
    let holes: [CourseHole]
}

struct CourseHole: Codable, Identifiable, Equatable {
    let id: Int
    let holeNumber: Int
    let par: Int
    let distance: Int
    let strokeIndex: Int?
}

struct RoundDraft: Codable, Identifiable, Equatable {
    let id: Int
    let courseId: Int
    let courseName: String
    let date: String
    let courseTeeId: Int?
    let tee: String
    let holes: [Hole]
    let status: String
    let currentHole: Int
    let expectedHoles: Int
    let updatedAt: Date?
}

typealias RoundDetail = RoundDraft

struct RoundSummary: Codable, Identifiable, Equatable, Hashable {
    let id: Int
    let courseId: Int
    let courseName: String
    let date: String
    let tee: String
    let totalScore: Int
    let scoreToPar: Int
    let status: String
    let holesPlayed: Int
    let expectedHoles: Int
}

struct ParTypeStats: Codable, Equatable {
    let holesPlayed: Int
    let averageScore: Double
    let averageScoreToPar: Double
}

/// `GET /api/statistics/round/{roundId}` — note the singular `round` segment.
struct RoundStatistics: Codable, Equatable {
    let roundId: Int
    let totalScore: Int
    let scoreToPar: Int
    let totalPutts: Int
    let averagePuttsPerHole: Double
    let girPercentage: Double
    let fairwayPercentage: Double?
    let totalPenalties: Int
    let eagles: Int
    let birdies: Int
    let pars: Int
    let bogeys: Int
    let doubleBogeysOrWorse: Int
    let par3: ParTypeStats
    let par4: ParTypeStats
    let par5: ParTypeStats
}

struct TrendPoint: Codable, Equatable, Identifiable {
    let roundId: Int
    let date: String
    let value: Double

    var id: Int { roundId }
    var day: Date? { APIClient.dayFormatter.date(from: date) }
}

struct RoundLengthStatistics: Codable, Equatable, Identifiable {
    let holeCount: Int
    let roundsPlayed: Int
    let averageScore: Double
    let bestScore: Int
    let averageScoreToPar: Double
    let recentFiveScoreToPar: Double?
    let recentTenScoreToPar: Double?

    var id: Int { holeCount }
}

struct PerformanceInsight: Codable, Equatable, Identifiable {
    let code: String
    let title: String
    let evidence: String
    let recommendation: String
    let severity: String

    var id: String { code }
}

/// Every field the server added after the first native slice is optional, so an
/// older or trimmed response still decodes.
struct OverviewStatistics: Codable, Equatable {
    let roundsPlayed: Int
    let averageScore: Double
    let bestScore: Int
    let averagePutts: Double
    let averageGirPercentage: Double
    let averageFairwayPercentage: Double?
    let recentRounds: [RoundSummary]
    let averagePuttsPerHole: Double?
    let recentFiveScoreToPar: Double?
    let recentTenScoreToPar: Double?
    let scoreTrend: [TrendPoint]?
    let girTrend: [TrendPoint]?
    let puttsTrend: [TrendPoint]?
    let scoreToParPerHoleTrend: [TrendPoint]?
    let puttsPerHoleTrend: [TrendPoint]?
    let byRoundLength: [RoundLengthStatistics]?
    let insights: [PerformanceInsight]?
}

/// Cursor-paged round list from `GET /api/rounds/page`.
struct RoundPage: Codable, Equatable {
    let items: [RoundSummary]
    let nextCursor: Int?
}

/// Server-side filters shared by the round list and the statistics overview.
struct RoundFilter: Equatable {
    var courseId: Int?
    var from: Date?
    var to: Date?
    var holeCount: Int?

    static let none = RoundFilter()

    var isActive: Bool {
        courseId != nil || from != nil || to != nil || holeCount != nil
    }

    /// Query items in the shape `RoundQueryDto` / `StatisticsQueryDto` expect.
    var queryItems: [URLQueryItem] {
        var items: [URLQueryItem] = []
        if let courseId { items.append(URLQueryItem(name: "courseId", value: "\(courseId)")) }
        if let from { items.append(URLQueryItem(name: "from", value: APIClient.dayFormatter.string(from: from))) }
        if let to { items.append(URLQueryItem(name: "to", value: APIClient.dayFormatter.string(from: to))) }
        if let holeCount { items.append(URLQueryItem(name: "holeCount", value: "\(holeCount)")) }
        return items
    }
}

struct UpdateProfileRequest: Encodable { let displayName: String }

struct PracticeSession: Codable, Identifiable, Equatable {
    let id: Int
    let focusCode: String
    let drillTitle: String
    let minutes: Int
    let startedAt: Date
    let completedAt: Date?
    let result: String?
    let notes: String?
}

struct PracticeSessionRequest: Encodable {
    let focusCode: String
    let drillTitle: String
    let minutes: Int
}

struct PracticeCompletionRequest: Encodable {
    let result: String?
    let notes: String?
}

struct PasswordResetRequest: Encodable { let email: String }
struct DeleteAccountRequest: Encodable { let password: String; let confirmation: String }

struct ChangePasswordRequest: Encodable {
    let currentPassword: String
    let newPassword: String
}

struct VerifyEmailRequest: Encodable { let token: String }

struct ResetPasswordRequest: Encodable {
    let token: String
    let newPassword: String
}

/// The server requires exactly 18 holes for a custom course.
struct CourseHoleCreateRequest: Encodable {
    let holeNumber: Int
    let par: Int
    let distance: Int
}

struct CourseCreateRequest: Encodable {
    let name: String
    let location: String
    let holes: [CourseHoleCreateRequest]
}

struct CourseUpdateRequest: Encodable {
    let name: String
    let location: String
}

struct RoundUpdateRequest: Encodable {
    let date: String
    let courseTeeId: Int?
    let tee: String?
    let expectedUpdatedAt: Date?
}

struct Hole: Codable, Identifiable, Equatable {
    let id: Int
    let holeNumber: Int
    let par: Int
    let score: Int
    let putts: Int
    let gir: Bool
    let fairwayHit: Bool?
    let penalty: Int
}

struct RoundStartRequest: Encodable {
    let courseId: Int
    let date: String
    let courseTeeId: Int?
    let tee: String?
}

struct HoleUpsertRequest: Codable {
    let par: Int?
    let score: Int
    let putts: Int
    let gir: Bool
    let fairwayHit: Bool?
    let penalty: Int
    let checkExpected: Bool
    let expectedHole: Hole?
}
