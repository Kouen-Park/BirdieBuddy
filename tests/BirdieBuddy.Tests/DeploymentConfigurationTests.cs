using BirdieBuddy.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace BirdieBuddy.Tests;

public sealed class DeploymentConfigurationTests
{
    [Fact]
    public void ProductionConfiguration_RequiresDatabaseAndSecureUrls()
    {
        var missingDatabase = ProductionBuilder();
        Assert.Throws<InvalidOperationException>(() => missingDatabase.ValidateDeploymentConfiguration());

        var insecureUrl = ProductionBuilder();
        insecureUrl.Configuration["ConnectionStrings:DefaultConnection"] = "Host=db;Database=birdie;Username=app;Password=strong";
        insecureUrl.Configuration["Application:PublicBaseUrl"] = "http://birdie.example";
        Assert.Throws<InvalidOperationException>(() => insecureUrl.ValidateDeploymentConfiguration());

        var partialEmail = ProductionBuilder();
        partialEmail.Configuration["ConnectionStrings:DefaultConnection"] = "Host=db;Database=birdie;Username=app;Password=strong";
        partialEmail.Configuration["Email:Smtp:Host"] = "smtp.example";
        Assert.Throws<InvalidOperationException>(() => partialEmail.ValidateDeploymentConfiguration());

        var verificationWithoutEmail = ProductionBuilder();
        verificationWithoutEmail.Configuration["ConnectionStrings:DefaultConnection"] = "Host=db;Database=birdie;Username=app;Password=strong";
        verificationWithoutEmail.Configuration["Authentication:RequireVerifiedEmail"] = "true";
        Assert.Throws<InvalidOperationException>(() => verificationWithoutEmail.ValidateDeploymentConfiguration());
    }

    [Fact]
    public void ProductionConfiguration_AcceptsCompleteSecureSettings()
    {
        var builder = ProductionBuilder();
        builder.Configuration["ConnectionStrings:DefaultConnection"] = "Host=db;Database=birdie;Username=app;Password=strong";
        builder.Configuration["Application:PublicBaseUrl"] = "https://birdie.example";
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "https://otel.example";
        builder.Configuration["Email:Smtp:Host"] = "smtp.example";
        builder.Configuration["Email:From"] = "support@birdie.example";
        builder.Configuration["Email:Smtp:Port"] = "587";
        builder.Configuration["Authentication:RequireVerifiedEmail"] = "true";
        builder.ValidateDeploymentConfiguration();
    }

    private static WebApplicationBuilder ProductionBuilder() =>
        WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
}
