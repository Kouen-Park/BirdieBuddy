namespace BirdieBuddy.Services;

public sealed record ServiceError(int Status, string Code, string Title, string Detail);

public readonly record struct ServiceResult<T>(T? Value, ServiceError? Error)
{
    public bool IsSuccess => Error is null;

    public static ServiceResult<T> Success(T value) => new(value, null);
    public static ServiceResult<T> Failure(ServiceError error) => new(default, error);
}

public static class ServiceErrors
{
    public static ServiceError RoundNotFound() =>
        new(404, "round.not_found", "Round not found.", "Round not found.");

    public static ServiceError HoleNotFound() =>
        new(404, "round.hole_not_found", "Hole not found.", "Hole not found.");

    public static ServiceError CourseNotFound() =>
        new(404, "course.not_found", "Course not found.", "Course not found.");

    public static ServiceError PracticeNotFound() =>
        new(404, "practice.not_found", "Practice session not found.", "Practice session not found.");

    public static ServiceError RoundInvalid(string detail) =>
        new(400, "round.invalid", "Round request is invalid.", detail);

    public static ServiceError HoleInvalid(string detail) =>
        new(400, "round.hole_invalid", "Hole request is invalid.", detail);

    public static ServiceError RoundConflict() =>
        new(409, "round.save_conflict", "The record changed elsewhere.", RoundService.ConflictMessage);

    public static ServiceError CourseInvalid(string detail) =>
        new(400, "course.invalid", "Course request is invalid.", detail);

    public static ServiceError CourseInUse() =>
        new(409, "course.in_use", "Course could not be deleted.",
            "Cannot delete a course that has recorded rounds.");

    public static ServiceError PracticeInvalid(string detail) =>
        new(400, "practice.invalid", "Practice request is invalid.", detail);

    public static ServiceError RegistrationInvalid(string detail) =>
        new(400, "auth.registration_invalid", "Account could not be created.", detail);

    public static ServiceError EmailExists() =>
        new(409, "auth.email_exists", "Account could not be created.",
            "An account with this email already exists.");

    public static ServiceError AuthenticationRequired() =>
        new(401, "auth.required", "Authentication required.", "Sign in to access this resource.");

    public static ServiceError ProfileInvalid(string detail) =>
        new(400, "auth.profile_invalid", "Profile could not be updated.", detail);

    public static ServiceError PasswordChangeInvalid(string detail) =>
        new(400, "auth.password_change_invalid", "Password could not be changed.", detail);

    public static ServiceError ResetTokenInvalid(string detail) =>
        new(400, "auth.reset_token_invalid", "Password could not be reset.", detail);

    public static ServiceError VerificationTokenInvalid(string detail) =>
        new(400, "auth.verification_token_invalid", "Email could not be verified.", detail);

    public static ServiceError AccountDeletionInvalid(string detail) =>
        new(400, "auth.account_deletion_invalid", "Account could not be deleted.", detail);

    public static ServiceError TelemetryInvalid(string detail) =>
        new(400, "telemetry.invalid_event", "Telemetry event was not accepted.", detail);
}
