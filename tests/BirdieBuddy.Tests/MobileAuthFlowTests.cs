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
}
