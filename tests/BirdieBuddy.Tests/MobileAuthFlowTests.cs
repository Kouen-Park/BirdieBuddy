using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace BirdieBuddy.Tests;

public sealed class MobileAuthFlowTests
{
    [Fact]
    public async Task MobileSessionRefreshesRotatesAndRevokesTokens()
    {
        await using var app = new BirdieBuddyApplicationFactory();
        using var browser = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        var csrf = await browser.GetFromJsonAsync<JsonElement>("/api/security/csrf");
        browser.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        var credentials = new { email = "mobile@example.test", displayName = "Mobile Golfer", password = "BirdiePass123" };
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/auth/register", credentials)).StatusCode);

        using var client = app.CreateClient();
        var sessionResponse = await client.PostAsJsonAsync("/api/mobile/auth/session",
            new { email = credentials.email, password = credentials.password });
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        var session = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var access = session.GetProperty("accessToken").GetString()!;
        var refresh = session.GetProperty("refreshToken").GetString()!;
        Assert.Equal("Mobile Golfer", session.GetProperty("user").GetProperty("displayName").GetString());

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/rounds")).StatusCode);
        var rotatedResponse = await client.PostAsJsonAsync("/api/mobile/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.OK, rotatedResponse.StatusCode);
        var rotated = await rotatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(refresh, rotated.GetProperty("refreshToken").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/mobile/auth/refresh", new { refreshToken = refresh })).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rotated.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/mobile/auth/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/rounds")).StatusCode);
    }

    [Fact]
    public async Task MobileRegistrationIssuesTokensAndBearerWritesSkipAntiforgery()
    {
        await using var app = new BirdieBuddyApplicationFactory();
        using var client = app.CreateClient();

        var credentials = new { email = "mobile-signup@example.test", displayName = "Signup Golfer", password = "BirdiePass123" };
        var registration = await client.PostAsJsonAsync("/api/mobile/auth/register", credentials);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var session = await registration.Content.ReadFromJsonAsync<JsonElement>();
        var access = session.GetProperty("accessToken").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(session.GetProperty("refreshToken").GetString()));
        Assert.Equal("Signup Golfer", session.GetProperty("user").GetProperty("displayName").GetString());

        // A bearer credential is never ambient, so an unsafe mobile request must not need
        // an antiforgery token. Without this the whole native write path returns 400.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var profile = await client.PutAsJsonAsync("/api/auth/profile", new { displayName = "Renamed Golfer" });
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);

        // A cookie-authenticated browser request still must present one.
        using var browser = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        var csrf = await browser.GetFromJsonAsync<JsonElement>("/api/security/csrf");
        browser.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/auth/login",
            new { email = credentials.email, password = credentials.password })).StatusCode);
        browser.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await browser.PutAsJsonAsync("/api/auth/profile", new { displayName = "Forged Golfer" })).StatusCode);
    }
}
