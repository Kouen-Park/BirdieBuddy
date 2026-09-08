using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BirdieBuddy.Controllers;
using BirdieBuddy.Data;
using BirdieBuddy.Infrastructure;
using BirdieBuddy.Services;
using BirdieBuddy.DTOs;
using BirdieBuddy.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace BirdieBuddy.Tests;

public sealed class AuthFlowTests
{
    [Fact]
    public async Task CsrfAndLoginFlow_RejectsMissingTokenAndAcceptsValidCredentials()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration["Administration:ImportKey"] = "integration-admin-key";
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddBirdieBuddyControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("auth", limiter =>
        {
            limiter.PermitLimit = 100;
            limiter.Window = TimeSpan.FromMinutes(1);
        }));
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddSingleton<TestAccountEmailSender>();
        builder.Services.AddSingleton<IAccountEmailSender>(services => services.GetRequiredService<TestAccountEmailSender>());
        builder.Services.AddSingleton<IAdminKeyValidator, AdminKeyValidator>();
        builder.Services.AddSingleton<IOperationalMetrics, OperationalMetrics>();
        builder.Services.AddScoped<IProductTelemetryService, ProductTelemetryService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddScoped<IRoundService, RoundService>();

        await using var app = builder.Build();
        app.UseBirdieBuddySecurityHeaders();
        var projectDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (projectDirectory is not null && !File.Exists(Path.Combine(projectDirectory.FullName, "BirdieBuddy.csproj")))
            projectDirectory = projectDirectory.Parent;
        Assert.NotNull(projectDirectory);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(projectDirectory.FullName, "wwwroot"))
        });
        app.UseRouting();
        app.UseApiObservability();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            using var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
            var page = await client.GetAsync("/login.html");
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var contentSecurityPolicy = page.Headers.GetValues("Content-Security-Policy").Single();
            Assert.Contains("frame-ancestors 'none'", contentSecurityPolicy);
            Assert.Contains("script-src 'self'", contentSecurityPolicy);
            Assert.Contains("font-src 'self'", contentSecurityPolicy);
            Assert.DoesNotContain("cdn.jsdelivr.net", contentSecurityPolicy);
            Assert.DoesNotContain("fonts.googleapis.com", contentSecurityPolicy);
            Assert.DoesNotContain("fonts.gstatic.com", contentSecurityPolicy);
            Assert.Equal("nosniff", page.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.True(page.Headers.CacheControl?.NoCache);
            Assert.True(page.Headers.CacheControl?.MustRevalidate);
            foreach (var asset in new[]
                     {
                         "/css/styles.css?v=20260908-local-assets",
                         "/js/api.js?v=20260903-rail3",
                         "/vendor/chart.js/chart.umd.min.js?v=4.4.4",
                         "/vendor/fonts/outfit-400.ttf"
                     })
            {
                var response = await client.GetAsync(asset);
                response.EnsureSuccessStatusCode();
                Assert.True(response.Headers.CacheControl?.NoCache);
                var conditional = new HttpRequestMessage(HttpMethod.Get, asset);
                conditional.Headers.IfNoneMatch.Add(response.Headers.ETag!);
                var unchanged = await client.SendAsync(conditional);
                Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
                Assert.True(unchanged.Headers.CacheControl?.NoCache);
            }
            var csrf = await client.GetAsync("/api/security/csrf");
            Assert.Equal(HttpStatusCode.OK, csrf.StatusCode);
            Assert.Equal("application/json", csrf.Content.Headers.ContentType?.MediaType);
            Assert.True(csrf.Headers.CacheControl?.NoStore);
            var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            Assert.False(string.IsNullOrWhiteSpace(token));

            var credentials = new { email = "golfer@example.test", password = "BirdiePass123", displayName = "Test Golfer" };
            var missing = await client.PostAsJsonAsync("/api/auth/register", credentials);
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "invalid-token");
            var invalid = await client.PostAsJsonAsync("/api/auth/register", credentials);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
            var registered = await client.PostAsJsonAsync("/api/auth/register", credentials);
            Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
            var emailSender = app.Services.GetRequiredService<TestAccountEmailSender>();
            Assert.NotNull(emailSender.VerificationUrl);
            await RefreshToken(client);
            var verificationToken = TokenFrom(emailSender.VerificationUrl!);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/verify-email",
                new VerifyEmailDto(verificationToken))).StatusCode);

            // Tokens are bound to the current identity; refresh after sign-in and sign-out.
            await RefreshToken(client);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
            await RefreshToken(client);
            var denied = await client.PostAsJsonAsync("/api/auth/login", new { email = credentials.email, password = "Incorrect123" });
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            Assert.Equal("auth.invalid_credentials", (await denied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            var login = await client.PostAsJsonAsync("/api/auth/login", credentials);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            await RefreshToken(client);
            var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.Equal(credentials.email, me.GetProperty("email").GetString());
            Assert.True(me.GetProperty("emailVerified").GetBoolean());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin/operations")).StatusCode);
            client.DefaultRequestHeaders.Add("X-Admin-Key", "integration-admin-key");
            var operations = await client.GetFromJsonAsync<JsonElement>("/api/admin/operations");
            Assert.True(operations.GetProperty("requests").GetInt64() > 0);
            Assert.True(operations.GetProperty("routes").GetArrayLength() > 0);
            client.DefaultRequestHeaders.Remove("X-Admin-Key");

            var unknownReset = await client.PostAsJsonAsync("/api/auth/forgot-password",
                new RequestPasswordResetDto("missing@example.test"));
            Assert.Equal(HttpStatusCode.Accepted, unknownReset.StatusCode);
            var requestedReset = await client.PostAsJsonAsync("/api/auth/forgot-password",
                new RequestPasswordResetDto(credentials.email));
            Assert.Equal(HttpStatusCode.Accepted, requestedReset.StatusCode);
            var resetToken = TokenFrom(emailSender.ResetUrl!);
            const string newPassword = "ChangedPass456";
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/reset-password",
                new ResetPasswordDto(resetToken, newPassword))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password",
                new ResetPasswordDto(resetToken, "AnotherPass789"))).StatusCode);

            int courseId;
            int teeId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var tee = new CourseTee { Name = "Test", NineHoles = true,
                    CourseHoles = Enumerable.Range(1, 9).Select(n => new CourseHole { HoleNumber = n, Par = 4 }).ToList() };
                var course = new Course { Name = "HTTP test course", CourseTees = new() { tee } };
                db.Courses.Add(course);
                await db.SaveChangesAsync();
                courseId = course.Id;
                teeId = tee.Id;
            }
            await RefreshToken(client);
            var started = await client.PostAsJsonAsync("/api/rounds/drafts", new RoundStartDto(courseId, new(2026, 9, 3), teeId, null));
            Assert.Equal(HttpStatusCode.Created, started.StatusCode);
            var draft = await started.Content.ReadFromJsonAsync<RoundDetailDto>();
            var route = $"/api/rounds/{draft!.Id}/holes/by-number/1";
            var saved = await client.PutAsJsonAsync(route, new HoleUpsertDto(4, 4, 2, true, true, 0, true));
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            var stale = await client.PutAsJsonAsync(route, new HoleUpsertDto(4, 5, 2, false, false, 0, true));
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal("application/problem+json", stale.Content.Headers.ContentType?.MediaType);
            Assert.Equal("round.save_conflict", (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

            var resumeEventId = Guid.NewGuid();
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/telemetry/events",
                new ProductEventDto(resumeEventId, "draft_resumed", draft.Id, null))).StatusCode);
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/telemetry/events",
                new ProductEventDto(resumeEventId, "draft_resumed", draft.Id, null))).StatusCode);
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/telemetry/events",
                new ProductEventDto(Guid.NewGuid(), "hole_input_completed", draft.Id, 8000))).StatusCode);
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/telemetry/events",
                new ProductEventDto(Guid.NewGuid(), "hole_input_completed", draft.Id, 12000))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/telemetry/events",
                new ProductEventDto(Guid.NewGuid(), "email", draft.Id, null))).StatusCode);
            client.DefaultRequestHeaders.Add("X-Admin-Key", "integration-admin-key");
            var beta = await client.GetFromJsonAsync<JsonElement>("/api/admin/operations/beta");
            Assert.Equal(1, beta.GetProperty("startedRounds").GetInt32());
            Assert.Equal(1, beta.GetProperty("resumedRounds").GetInt32());
            Assert.Equal(2, beta.GetProperty("holeTimings").GetInt32());
            Assert.Equal(10, beta.GetProperty("medianHoleInputSeconds").GetDouble());
            client.DefaultRequestHeaders.Remove("X-Admin-Key");

            var export = await client.GetAsync("/api/auth/export");
            Assert.Equal(HttpStatusCode.OK, export.StatusCode);
            Assert.Equal("application/json", export.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", export.Content.Headers.ContentDisposition?.DispositionType);
            var exportText = await export.Content.ReadAsStringAsync();
            Assert.Contains("golfer@example.test", exportText);
            Assert.Contains("HTTP test course", exportText);
            Assert.DoesNotContain("passwordHash", exportText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("passwordSalt", exportText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("hole_input_completed", exportText);

            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/delete-account",
                new DeleteAccountDto(credentials.password, "delete"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/delete-account",
                new DeleteAccountDto("Incorrect123", "DELETE MY ACCOUNT"))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/delete-account",
                new DeleteAccountDto(newPassword, "DELETE MY ACCOUNT"))).StatusCode);
            var signedOut = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.Found, signedOut.StatusCode);
            Assert.Contains("login", signedOut.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Empty(await db.Users.ToListAsync());
                Assert.Empty(await db.Rounds.ToListAsync());
                Assert.Empty(await db.Holes.ToListAsync());
                Assert.Empty(await db.ProductEvents.ToListAsync());
            }
        }
        finally { await app.StopAsync(); }
    }

    private static async Task RefreshToken(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/security/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", response.GetProperty("token").GetString());
    }

    private static string TokenFrom(string url) => Uri.UnescapeDataString(
        new Uri(url).Query.TrimStart('?').Split('&').Single(x => x.StartsWith("token=")).Split('=', 2)[1]);

    private sealed class TestAccountEmailSender : IAccountEmailSender
    {
        public string? ResetUrl { get; private set; }
        public string? VerificationUrl { get; private set; }
        public Task SendPasswordResetAsync(string email, string resetUrl) { ResetUrl = resetUrl; return Task.CompletedTask; }
        public Task SendEmailVerificationAsync(string email, string verificationUrl) { VerificationUrl = verificationUrl; return Task.CompletedTask; }
    }
}
