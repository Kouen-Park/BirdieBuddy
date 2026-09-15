using System.Security.Cryptography;
using System.Text;
using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using Microsoft.EntityFrameworkCore;

namespace BirdieBuddy.Services;

public interface IMobileTokenService
{
    Task<ServiceResult<MobileSessionDto>> CreateSessionAsync(MobileSessionRequest request);
    Task<ServiceResult<MobileSessionDto>> RefreshAsync(MobileRefreshRequest request);
    Task RevokeAsync(int userId);
    Task<User?> FindAccessUserAsync(string rawToken);
}

public sealed class MobileTokenService : IMobileTokenService
{
    private const string AccessType = "mobile-access";
    private const string RefreshType = "mobile-refresh";
    private static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);
    private readonly ApplicationDbContext _context;
    private readonly IAuthService _authService;

    public MobileTokenService(ApplicationDbContext context, IAuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    public async Task<ServiceResult<MobileSessionDto>> CreateSessionAsync(MobileSessionRequest request)
    {
        var user = await _authService.ValidateLoginAsync(new LoginDto(request.Email, request.Password));
        if (user is null)
            return ServiceResult<MobileSessionDto>.Failure(ServiceErrors.AuthenticationRequired("Email or password is incorrect."));

        var entity = await _context.Users.FindAsync(user.Id);
        if (entity is null)
            return ServiceResult<MobileSessionDto>.Failure(ServiceErrors.AuthenticationRequired());

        return ServiceResult<MobileSessionDto>.Success(await IssueAsync(entity));
    }

    public async Task<ServiceResult<MobileSessionDto>> RefreshAsync(MobileRefreshRequest request)
    {
        var token = await FindAsync(request.RefreshToken, RefreshType, includeUser: true);
        if (token?.User is null)
            return ServiceResult<MobileSessionDto>.Failure(ServiceErrors.AuthenticationRequired("The refresh token is invalid or expired."));

        token.UsedAt = DateTime.UtcNow;
        return ServiceResult<MobileSessionDto>.Success(await IssueAsync(token.User));
    }

    public async Task RevokeAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var tokens = await _context.AccountTokens
            .Where(t => t.UserId == userId && (t.Type == AccessType || t.Type == RefreshType) && t.UsedAt == null)
            .ToListAsync();
        foreach (var token in tokens) token.UsedAt = now;
        await _context.SaveChangesAsync();
    }

    public async Task<User?> FindAccessUserAsync(string rawToken)
    {
        var token = await FindAsync(rawToken, AccessType, includeUser: true);
        return token?.User;
    }

    private async Task<MobileSessionDto> IssueAsync(User user)
    {
        var now = DateTime.UtcNow;
        var accessExpires = now.Add(AccessLifetime);
        var refreshExpires = now.Add(RefreshLifetime);
        var access = NewToken();
        var refresh = NewToken();

        _context.AccountTokens.AddRange(
            new AccountToken { UserId = user.Id, Type = AccessType, TokenHash = Hash(access), CreatedAt = now, ExpiresAt = accessExpires },
            new AccountToken { UserId = user.Id, Type = RefreshType, TokenHash = Hash(refresh), CreatedAt = now, ExpiresAt = refreshExpires });
        await _context.SaveChangesAsync();

        return new(access, refresh, accessExpires, refreshExpires,
            new CurrentUserDto(user.Id, user.Email, user.DisplayName, user.EmailVerifiedAt is not null));
    }

    private async Task<AccountToken?> FindAsync(string? raw, string type, bool includeUser)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var query = _context.AccountTokens.AsQueryable();
        if (includeUser) query = query.Include(t => t.User);
        return await query.FirstOrDefaultAsync(t => t.TokenHash == Hash(raw.Trim()) &&
            t.Type == type && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow);
    }

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
