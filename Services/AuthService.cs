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
}

public sealed class AuthService : IAuthService
{
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

    private static byte[] HashPassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

    private static CurrentUserDto ToDto(User user) =>
        new(user.Id, user.Email, user.DisplayName);
}
