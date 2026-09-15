using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BirdieBuddy.Data;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BirdieBuddy.Tests;

public sealed class ProgramPipelineTests : IClassFixture<BirdieBuddyApplicationFactory>
{
    private readonly BirdieBuddyApplicationFactory _factory;

    public ProgramPipelineTests(BirdieBuddyApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task RealProgram_ExposesHealthSecurityHeadersAndProblemDetails()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var page = await client.GetAsync("/login.html");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("script-src 'self'", page.Headers.GetValues("Content-Security-Policy").Single());

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);

        var publicCourses = await client.GetAsync("/api/courses/page?limit=1");
        Assert.Equal(HttpStatusCode.OK, publicCourses.StatusCode);

        var unauthorized = await client.GetAsync("/api/rounds");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("application/problem+json", unauthorized.Content.Headers.ContentType?.MediaType);
        var unauthorizedProblem = await unauthorized.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("auth.required", unauthorizedProblem.GetProperty("code").GetString());
        Assert.Equal("/api/rounds", unauthorizedProblem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(unauthorizedProblem.GetProperty("traceId").GetString()));
        Assert.True(unauthorized.Headers.Contains("X-Trace-Id"));

        var csrfFailure = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "pipeline@example.test",
            displayName = "Pipeline Test",
            password = "PipelinePass123"
        });
        Assert.Equal(HttpStatusCode.BadRequest, csrfFailure.StatusCode);
        Assert.Equal("application/problem+json", csrfFailure.Content.Headers.ContentType?.MediaType);
        Assert.Equal("security.csrf_invalid",
            (await csrfFailure.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task RealProgram_RateLimitUsesTheSharedProblemDetailsContract()
    {
        await using var factory = new BirdieBuddyApplicationFactory(authPermitLimit: 1);
        using var client = factory.CreateClient();
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/security/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());

        var credentials = new { email = "missing@example.test", password = "MissingPass123" };
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/login", credentials)).StatusCode);

        var limited = await client.PostAsJsonAsync("/api/auth/login", credentials);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        Assert.Equal("rate_limit.exceeded",
            (await limited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}

public sealed class BirdieBuddyApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"birdiebuddy-program-{Guid.NewGuid():N}";
    private readonly int _authPermitLimit;

    public BirdieBuddyApplicationFactory() : this(100) { }

    internal BirdieBuddyApplicationFactory(int authPermitLimit) => _authPermitLimit = authPermitLimit;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=birdiebuddy_test_program;Username=postgres",
                ["Administration:ImportKey"] = "integration-admin-key",
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["RateLimiting:AuthPermitLimit"] = _authPermitLimit.ToString()
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.RemoveAll<IAccountEmailSender>();
            services.AddSingleton<CapturingAccountEmailSender>();
            services.AddSingleton<IAccountEmailSender>(provider =>
                provider.GetRequiredService<CapturingAccountEmailSender>());
        });
    }
}

public sealed class CapturingAccountEmailSender : IAccountEmailSender
{
    public string? ResetUrl { get; private set; }
    public string? VerificationUrl { get; private set; }

    public Task SendPasswordResetAsync(string email, string resetUrl)
    {
        ResetUrl = resetUrl;
        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(string email, string verificationUrl)
    {
        VerificationUrl = verificationUrl;
        return Task.CompletedTask;
    }
}
