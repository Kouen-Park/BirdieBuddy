namespace BirdieBuddy.DTOs;

public record RegisterDto(string Email, string DisplayName, string Password);

public record LoginDto(string Email, string Password);

public record CurrentUserDto(int Id, string Email, string DisplayName, bool EmailVerified = false);

public record UpdateProfileDto(string DisplayName);

public record ChangePasswordDto(string CurrentPassword, string NewPassword);

public record RequestPasswordResetDto(string Email);

public record ResetPasswordDto(string Token, string NewPassword);

public record VerifyEmailDto(string Token);

public record DeleteAccountDto(string Password, string Confirmation);

public record AccountExportDto(
    int SchemaVersion,
    DateTime ExportedAt,
    CurrentUserDto Profile,
    List<CourseDto> CustomCourses,
    List<RoundDetailDto> Rounds,
    List<ProductEventExportDto> ProductEvents);

public record ProductEventExportDto(string EventType, int? RoundId, int? DurationMs, DateTime OccurredAt);
