using BirdieBuddy.Controllers;
using BirdieBuddy.Data;
using BirdieBuddy.DTOs;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BirdieBuddy.Tests;

public sealed class EmailVerificationPolicyTests
{
    [Fact]
    public async Task RequiredVerification_DoesNotSignInNewOrUnverifiedAccount()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var auth = new AuthService(db);
        var email = new CapturingEmailSender();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:RequireVerifiedEmail"] = "true",
            ["Application:PublicBaseUrl"] = "https://birdie.example"
        }).Build();
        var controller = new AuthController(auth, email, configuration, NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("birdie.example");

        var registration = await controller.Register(new RegisterDto("new@example.test", "New Golfer", "BirdiePass123"));
        var accepted = Assert.IsType<AcceptedResult>(registration.Result);
        Assert.NotNull(accepted.Value);
        Assert.NotNull(email.VerificationUrl);
        Assert.False(controller.HttpContext.User.Identity?.IsAuthenticated ?? false);

        var login = await controller.Login(new LoginDto("new@example.test", "BirdiePass123"));
        var blocked = Assert.IsType<ObjectResult>(login.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, blocked.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(blocked.Value);
        Assert.Equal("auth.email_unverified", problem.Extensions["code"]);
    }

    private sealed class CapturingEmailSender : IAccountEmailSender
    {
        public string? VerificationUrl { get; private set; }
        public Task SendPasswordResetAsync(string email, string resetUrl) => Task.CompletedTask;
        public Task SendEmailVerificationAsync(string email, string verificationUrl)
        { VerificationUrl = verificationUrl; return Task.CompletedTask; }
    }
}
