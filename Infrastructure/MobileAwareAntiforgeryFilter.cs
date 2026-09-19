using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BirdieBuddy.Infrastructure;

/// <summary>
/// Validates the antiforgery token for unsafe requests, except when the caller is
/// authenticated by a mobile bearer token.
///
/// CSRF protection exists because a browser attaches the authentication cookie to a
/// cross-site request automatically. A bearer token is never ambient: it is attached
/// only by code that already holds it, and a cross-origin caller cannot set the
/// Authorization header without a CORS preflight this API does not grant. A request
/// that arrives with a VALID mobile access token therefore cannot be forged, while a
/// cookie-authenticated browser request still requires the token.
///
/// This replaces the global <see cref="AutoValidateAntiforgeryTokenAttribute"/>, which
/// validated by HTTP method alone and so rejected every write from the native client.
/// </summary>
public sealed class MobileAwareAntiforgeryFilter : IAsyncAuthorizationFilter
{
    public const string MobileAuthSchemeClaim = "birdiebuddy.auth-scheme";
    public const string MobileAuthSchemeValue = "mobile-bearer";

    private readonly IAntiforgery _antiforgery;

    public MobileAwareAntiforgeryFilter(IAntiforgery antiforgery) => _antiforgery = antiforgery;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!ShouldValidate(context)) return;

        try
        {
            await _antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            // AntiforgeryProblemDetailsFilter turns this marker into the application's
            // problem+json contract.
            context.Result = new AntiforgeryFailedResult();
        }
    }

    private static bool ShouldValidate(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return false;
        }

        if (IsExempt(context)) return false;

        return !IsMobileBearerRequest(context.HttpContext);
    }

    private static bool IsExempt(AuthorizationFilterContext context)
    {
        // [IgnoreAntiforgeryToken] normally wins through MVC's policy-override plumbing.
        // This filter is type-activated (so IAntiforgery can be injected), which hides it
        // from that plumbing, so the attribute is honoured explicitly instead.
        if (context.Filters.Any(filter => filter is IgnoreAntiforgeryTokenAttribute)) return true;
        return context.ActionDescriptor.EndpointMetadata
            .Any(metadata => metadata is IgnoreAntiforgeryTokenAttribute);
    }

    public static bool IsMobileBearerRequest(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
        && context.User.HasClaim(MobileAuthSchemeClaim, MobileAuthSchemeValue);

    private sealed class AntiforgeryFailedResult : BadRequestResult, IAntiforgeryValidationFailedResult;
}
