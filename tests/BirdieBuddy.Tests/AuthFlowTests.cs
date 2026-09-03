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
            Assert.Contains("frame-ancestors 'none'", page.Headers.GetValues("Content-Security-Policy").Single());
            Assert.Equal("nosniff", page.Headers.GetValues("X-Content-Type-Options").Single());
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

            // Tokens are bound to the current identity; refresh after sign-in and sign-out.
            await RefreshToken(client);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
            await RefreshToken(client);
            var denied = await client.PostAsJsonAsync("/api/auth/login", new { email = credentials.email, password = "Incorrect123" });
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            var login = await client.PostAsJsonAsync("/api/auth/login", credentials);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.Equal(credentials.email, me.GetProperty("email").GetString());

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
        }
        finally { await app.StopAsync(); }
    }

    private static async Task RefreshToken(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/security/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", response.GetProperty("token").GetString());
    }
}
