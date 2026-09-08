namespace BirdieBuddy.Infrastructure;

public static class DeploymentConfiguration
{
    public static void ValidateDeploymentConfiguration(this WebApplicationBuilder builder)
    {
        var connection = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        if (!builder.Environment.IsDevelopment() &&
            connection.Contains("Password=postgres", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The local development database password cannot be used in production.");

        ValidateHttpsUri(builder, "Application:PublicBaseUrl");
        ValidateHttpsUri(builder, "OTEL_EXPORTER_OTLP_ENDPOINT", allowLocalHttp: true);

        var smtpHost = builder.Configuration["Email:Smtp:Host"];
        var emailFrom = builder.Configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(smtpHost) != string.IsNullOrWhiteSpace(emailFrom))
            throw new InvalidOperationException("Email:Smtp:Host and Email:From must be configured together.");
        var smtpPort = builder.Configuration.GetValue<int?>("Email:Smtp:Port");
        if (smtpPort is <= 0 or > 65535)
            throw new InvalidOperationException("Email:Smtp:Port must be between 1 and 65535.");
    }

    private static void ValidateHttpsUri(WebApplicationBuilder builder, string key, bool allowLocalHttp = false)
    {
        var value = builder.Configuration[key];
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"{key} must be an absolute URL.");
        var local = uri.IsLoopback && allowLocalHttp;
        if (!builder.Environment.IsDevelopment() && uri.Scheme != Uri.UriSchemeHttps && !local)
            throw new InvalidOperationException($"{key} must use HTTPS in production.");
    }
}
