using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using Microsoft.EntityFrameworkCore;

namespace BirdieBuddy.Services;

public interface IAuthService
{
    Task<(CurrentUserDto? User, string? Error)> RegisterAsync(RegisterDto dto);
    Task<CurrentUserDto?> ValidateLoginAsync(LoginDto dto);
    Task<CurrentUserDto?> GetByIdAsync(int id);
    Task<(CurrentUserDto? User, string? Error)> UpdateProfileAsync(int id, UpdateProfileDto dto);
    Task<string?> ChangePasswordAsync(int id, ChangePasswordDto dto);
    Task<(string? Email, string? Token)> CreatePasswordResetAsync(string email);
    Task<string?> ResetPasswordAsync(ResetPasswordDto dto);
    Task<(string? Email, string? Token)> CreateEmailVerificationAsync(int id);
    Task<string?> VerifyEmailAsync(VerifyEmailDto dto);
    Task<AccountExportDto?> ExportAsync(int id);
    Task<string?> DeleteAccountAsync(int id, DeleteAccountDto dto);
}

public sealed class AuthService : IAuthService
{
    private const string PasswordResetToken = "password-reset";
    private const string EmailVerificationToken = "email-verification";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 120_000;
    private readonly ApplicationDbContext _context;

    public AuthService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(CurrentUserDto? User, string? Error)> RegisterAsync(RegisterDto dto)
    {
        var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
        var displayName = (dto.DisplayName ?? string.Empty).Trim();
        var password = dto.Password ?? string.Empty;

        if (!new EmailAddressAttribute().IsValid(email))
            return (null, "Enter a valid email address.");
        if (displayName.Length is < 2 or > 80)
            return (null, "Display name must be between 2 and 80 characters.");
        if (password.Length < 8 || !Regex.IsMatch(password, "[A-Za-z]") || !Regex.IsMatch(password, "\\d"))
            return (null, "Password must be at least 8 characters and include a letter and a number.");
        if (await _context.Users.AnyAsync(u => u.Email == email))
            return (null, "An account with this email already exists.");

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = HashPassword(password, salt);
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordSalt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(hash),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return (ToDto(user), null);
    }

    public async Task<CurrentUserDto?> ValidateLoginAsync(LoginDto dto)
    {
        var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null) return null;

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(user.PasswordSalt);
            expectedHash = Convert.FromBase64String(user.PasswordHash);
        }
        catch (FormatException)
        {
            return null;
        }

        var actualHash = HashPassword(dto.Password, salt);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash) ? ToDto(user) : null;
    }

    public async Task<CurrentUserDto?> GetByIdAsync(int id)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        return user is null ? null : ToDto(user);
    }

    public async Task<(CurrentUserDto? User, string? Error)> UpdateProfileAsync(int id, UpdateProfileDto dto)
    {
        var name = (dto.DisplayName ?? string.Empty).Trim();
        if (name.Length is < 2 or > 80) return (null, "Display name must be between 2 and 80 characters.");
        var user = await _context.Users.FindAsync(id);
        if (user is null) return (null, "Account not found.");
        user.DisplayName = name;
        await _context.SaveChangesAsync();
        return (ToDto(user), null);
    }

    public async Task<string?> ChangePasswordAsync(int id, ChangePasswordDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user is null) return "Account not found.";
        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(user.PasswordSalt);
            expected = Convert.FromBase64String(user.PasswordHash);
        }
        catch (FormatException) { return "Current password is incorrect."; }
        if (!CryptographicOperations.FixedTimeEquals(HashPassword(dto.CurrentPassword ?? string.Empty, salt), expected))
            return "Current password is incorrect.";
        var password = dto.NewPassword ?? string.Empty;
        if (password.Length < 8 || !Regex.IsMatch(password, "[A-Za-z]") || !Regex.IsMatch(password, "\\d"))
            return "New password must be at least 8 characters and include a letter and a number.";
        var newSalt = RandomNumberGenerator.GetBytes(SaltSize);
        user.PasswordSalt = Convert.ToBase64String(newSalt);
        user.PasswordHash = Convert.ToBase64String(HashPassword(password, newSalt));
        await _context.SaveChangesAsync();
        return null;
    }

    public async Task<(string? Email, string? Token)> CreatePasswordResetAsync(string email)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalized);
        if (user is null) return (null, null);
        var token = await CreateTokenAsync(user, PasswordResetToken, TimeSpan.FromMinutes(30));
        return (user.Email, token);
    }

    public async Task<string?> ResetPasswordAsync(ResetPasswordDto dto)
    {
        var passwordError = ValidatePassword(dto.NewPassword, "New password");
        if (passwordError is not null) return passwordError;
        var token = await FindUsableTokenAsync(dto.Token, PasswordResetToken);
        if (token is null) return "This reset link is invalid or has expired.";
        SetPassword(token.User, dto.NewPassword);
        token.UsedAt = DateTime.UtcNow;
        foreach (var other in await _context.AccountTokens.Where(t => t.UserId == token.UserId &&
                     t.Type == PasswordResetToken && t.UsedAt == null).ToListAsync())
            other.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return null;
    }

    public async Task<(string? Email, string? Token)> CreateEmailVerificationAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user is null || user.EmailVerifiedAt is not null) return (null, null);
        var token = await CreateTokenAsync(user, EmailVerificationToken, TimeSpan.FromHours(24));
        return (user.Email, token);
    }

    public async Task<string?> VerifyEmailAsync(VerifyEmailDto dto)
    {
        var token = await FindUsableTokenAsync(dto.Token, EmailVerificationToken);
        if (token is null) return "This verification link is invalid or has expired.";
        token.User.EmailVerifiedAt ??= DateTime.UtcNow;
        token.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return null;
    }

    public async Task<AccountExportDto?> ExportAsync(int id)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return null;
        var courses = await _context.Courses.AsNoTracking()
            .Where(c => c.UserId == id).Include(c => c.CourseTees).ThenInclude(t => t.CourseHoles)
            .OrderBy(c => c.Name).ToListAsync();
        var rounds = await _context.Rounds.AsNoTracking()
            .Where(r => r.UserId == id).Include(r => r.Course).Include(r => r.CourseTee)
            .ThenInclude(t => t!.CourseHoles).Include(r => r.Holes)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.Id).ToListAsync();
        var productEvents = await _context.ProductEvents.AsNoTracking().Where(e => e.UserId == id)
            .OrderBy(e => e.OccurredAt).Select(e => new ProductEventExportDto(
                e.EventType, e.RoundId, e.DurationMs, e.OccurredAt)).ToListAsync();
        return new(1, DateTime.UtcNow, ToDto(user),
            courses.Select(c =>
            {
                var tees = c.CourseTees.Select(t => new CourseTeeDto(t.Id, t.Name, t.CourseType, t.Gender,
                    t.NineHoles, t.Rating, t.Slope, t.Colour, t.TotalPar, t.FrontNinePar, t.BackNinePar,
                    t.FrontNineMetres, t.BackNineMetres, t.CourseHoles.OrderBy(h => h.HoleNumber)
                        .Select(h => new CourseHoleDto(h.Id, h.HoleNumber, h.Par, h.Distance, h.StrokeIndex)).ToList())).ToList();
                return new CourseDto(c.Id, c.GolfNzClubId, c.Name, c.Location, tees,
                    tees.FirstOrDefault()?.Holes ?? new List<CourseHoleDto>());
            }).ToList(),
            rounds.Select(r => new RoundDetailDto(r.Id, r.CourseId, r.Course?.Name ?? "", DateOnly.FromDateTime(r.Date),
                r.CourseTeeId, r.CourseTee?.Name ?? r.LegacyTee ?? "Unknown",
                r.Holes.OrderBy(h => h.HoleNumber).Select(h => new HoleDto(h.Id, h.HoleNumber, h.Par, h.Score,
                    h.Putts, h.GIR, h.FairwayHit, h.Penalty)).ToList(), r.Status.ToString(), r.CurrentHole,
                r.CourseTee?.CourseHoles.Count > 0 ? r.CourseTee.CourseHoles.Count : r.Holes.Count,
                r.UpdatedAt, r.CourseTee?.CourseHoles.OrderBy(h => h.HoleNumber).Select(h => h.HoleNumber).ToList())).ToList(),
            productEvents);
    }

    public async Task<string?> DeleteAccountAsync(int id, DeleteAccountDto dto)
    {
        if (!string.Equals(dto.Confirmation?.Trim(), "DELETE MY ACCOUNT", StringComparison.Ordinal))
            return "Type DELETE MY ACCOUNT exactly to confirm.";
        var user = await _context.Users.FindAsync(id);
        if (user is null) return "Account not found.";
        if (!PasswordMatches(user, dto.Password ?? string.Empty)) return "Password is incorrect.";
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;
        try
        {
            // A user-owned course cannot be removed while one of its rounds exists.
            // Delete rounds first and keep both operations atomic on PostgreSQL.
            var productEvents = await _context.ProductEvents.Where(e => e.UserId == id).ToListAsync();
            _context.ProductEvents.RemoveRange(productEvents);
            await _context.SaveChangesAsync();
            var holes = await _context.Holes.Where(h => h.Round!.UserId == id).ToListAsync();
            _context.Holes.RemoveRange(holes);
            await _context.SaveChangesAsync();
            var rounds = await _context.Rounds.Where(r => r.UserId == id).ToListAsync();
            _context.Rounds.RemoveRange(rounds);
            await _context.SaveChangesAsync();
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
        return null;
    }

    private static byte[] HashPassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

    private async Task<string> CreateTokenAsync(User user, string type, TimeSpan lifetime)
    {
        var now = DateTime.UtcNow;
        foreach (var existing in await _context.AccountTokens.Where(t => t.UserId == user.Id &&
                     t.Type == type && t.UsedAt == null).ToListAsync())
            existing.UsedAt = now;
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _context.AccountTokens.Add(new AccountToken
        {
            UserId = user.Id, Type = type, TokenHash = HashToken(raw), CreatedAt = now, ExpiresAt = now.Add(lifetime)
        });
        await _context.SaveChangesAsync();
        return raw;
    }

    private async Task<AccountToken?> FindUsableTokenAsync(string? raw, string type)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var hash = HashToken(raw.Trim());
        return await _context.AccountTokens.Include(t => t.User).FirstOrDefaultAsync(t =>
            t.TokenHash == hash && t.Type == type && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow);
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string? ValidatePassword(string? password, string label) =>
        (password ?? string.Empty).Length < 8 || !Regex.IsMatch(password ?? string.Empty, "[A-Za-z]") ||
        !Regex.IsMatch(password ?? string.Empty, "\\d")
            ? $"{label} must be at least 8 characters and include a letter and a number."
            : null;

    private static void SetPassword(User user, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        user.PasswordSalt = Convert.ToBase64String(salt);
        user.PasswordHash = Convert.ToBase64String(HashPassword(password, salt));
    }

    private static bool PasswordMatches(User user, string password)
    {
        try
        {
            var salt = Convert.FromBase64String(user.PasswordSalt);
            var expected = Convert.FromBase64String(user.PasswordHash);
            return CryptographicOperations.FixedTimeEquals(HashPassword(password, salt), expected);
        }
        catch (FormatException) { return false; }
    }

    private static CurrentUserDto ToDto(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.EmailVerifiedAt is not null);
}
