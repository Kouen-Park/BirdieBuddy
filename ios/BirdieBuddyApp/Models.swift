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

struct RoundSummary: Codable, Identifiable, Equatable {
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

struct OverviewStatistics: Codable, Equatable {
    let roundsPlayed: Int
    let averageScore: Double
    let bestScore: Int
    let averagePutts: Double
    let averageGirPercentage: Double
    let averageFairwayPercentage: Double?
    let recentRounds: [RoundSummary]
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
