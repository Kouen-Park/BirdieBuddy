using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BirdieBuddy.DTOs;

public record RegisterDto(
    [param: Required, EmailAddress, StringLength(320)] string? Email,
    [param: Required, StringLength(80, MinimumLength = 2)] string? DisplayName,
    [param: Required, StringLength(128, MinimumLength = 8), RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$")] string? Password);

public record LoginDto(
    [param: Required, EmailAddress, StringLength(320)] string? Email,
    [param: Required, StringLength(128, MinimumLength = 1)] string? Password);

public record MobileSessionRequest(
    [param: Required, EmailAddress, StringLength(320)] string? Email,
    [param: Required, StringLength(128, MinimumLength = 1)] string? Password);

public record MobileRefreshRequest(
    [param: Required, StringLength(128, MinimumLength = 32)] string? RefreshToken);

public record MobileSessionDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    CurrentUserDto User);

public record CurrentUserDto(int Id, string Email, string DisplayName, bool EmailVerified = false,
    [property: JsonIgnore] int SessionVersion = 0);

public record UpdateProfileDto([param: Required, StringLength(80, MinimumLength = 2)] string? DisplayName);

public record ChangePasswordDto(
    [param: Required, StringLength(128, MinimumLength = 1)] string? CurrentPassword,
    [param: Required, StringLength(128, MinimumLength = 8), RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$")] string? NewPassword);

public record RequestPasswordResetDto([param: Required, EmailAddress, StringLength(320)] string? Email);

public record ResetPasswordDto(
    [param: Required, StringLength(128, MinimumLength = 32)] string? Token,
    [param: Required, StringLength(128, MinimumLength = 8), RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$")] string? NewPassword);

public record VerifyEmailDto([param: Required, StringLength(128, MinimumLength = 32)] string? Token);

public record DeleteAccountDto(
    [param: Required, StringLength(128, MinimumLength = 1)] string? Password,
    [param: Required, StringLength(32, MinimumLength = 1)] string? Confirmation);

public record AccountExportDto(
    int SchemaVersion,
    DateTime ExportedAt,
    CurrentUserDto Profile,
    List<CourseDto> CustomCourses,
    List<RoundDetailDto> Rounds,
    List<ProductEventExportDto> ProductEvents);

public record ProductEventExportDto(string EventType, int? RoundId, int? DurationMs, DateTime OccurredAt);
