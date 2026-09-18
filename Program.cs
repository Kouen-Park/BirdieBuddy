using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Data;
using BirdieBuddy.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Security.Cryptography;
using BirdieBuddy.Infrastructure;
using BirdieBuddy.Models;

var builder = WebApplication.CreateBuilder(args);
builder.ValidateDeploymentConfiguration();
builder.AddBirdieBuddyTelemetry();

builder.Services.AddBirdieBuddyControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<WriteConflictHandler>();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "birdiebuddy.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        await ApiErrors.WriteProblemAsync(context.HttpContext, StatusCodes.Status429TooManyRequests,
            "rate_limit.exceeded", "Too many requests.",
            "Please wait a moment before trying again.", token);
    };
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 8),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddHttpContextAccessor();
var dataProtection = builder.Services.AddDataProtection();
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
if (!string.IsNullOrWhiteSpace(keyRingPath))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "birdiebuddy.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.LoginPath = "/login.html";
        options.Events.OnRedirectToLogin = async context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                await ApiErrors.WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                    "auth.required", "Authentication required.",
                    "Sign in to access this resource.", context.HttpContext.RequestAborted);
                return;
            }

            context.Response.Redirect(options.LoginPath);
        };
        options.Events.OnRedirectToAccessDenied = async context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                await ApiErrors.WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                    "auth.forbidden", "Access denied.",
                    "You do not have permission to access this resource.", context.HttpContext.RequestAborted);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        };
        options.Events.OnValidatePrincipal = SessionCookieValidator.ValidateAsync;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddCheck<BirdieBuddy.Services.DatabaseHealthCheck>("database");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Database=birdiebuddy_dev;Username=postgres"));

builder.Services.AddScoped<IGolfNzCourseImporter, GolfNzCourseImporter>();
builder.Services.AddSingleton<IGolfNzImportJob, GolfNzImportJob>();

builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IRoundQueryService, RoundQueryService>();
builder.Services.AddScoped<ILiveRoundService, LiveRoundService>();
builder.Services.AddScoped<IRoundLifecycleService, RoundLifecycleService>();
builder.Services.AddScoped<ICompletedRoundEditor, CompletedRoundEditor>();
builder.Services.AddScoped<IRoundService>(services => new RoundService(
    services.GetRequiredService<IRoundQueryService>(),
    services.GetRequiredService<ILiveRoundService>(),
    services.GetRequiredService<IRoundLifecycleService>(),
    services.GetRequiredService<ICompletedRoundEditor>()));
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMobileTokenService, MobileTokenService>();
builder.Services.AddScoped<IAccountEmailSender, SmtpAccountEmailSender>();
builder.Services.AddSingleton<IAdminKeyValidator, AdminKeyValidator>();
builder.Services.AddSingleton<IOperationalMetrics, OperationalMetrics>();
builder.Services.AddScoped<IProductTelemetryService, ProductTelemetryService>();
builder.Services.AddScoped<IPracticeSessionService, PracticeSessionService>();

var app = builder.Build();

var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Apply pending schema migrations and import Golf NZ data on startup. The
// advisory lock prevents multiple Render instances from importing concurrently.
if (!app.Configuration.GetValue("EF_DESIGNTIME", false) &&
    app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var connection = context.Database.GetDbConnection();
    await connection.OpenAsync();
    await using var migrationLock = connection.CreateCommand();
    migrationLock.CommandText = "SELECT pg_advisory_lock(42424201)";
    await migrationLock.ExecuteNonQueryAsync();
    try
    {
        await context.Database.MigrateAsync();

        if (app.Configuration.GetValue("Database:ImportGolfNzOnStartup", true))
        {
            var dataPath = Path.Combine(app.Environment.ContentRootPath, "scripts", "golf_nz_courses.json");
            if (!File.Exists(dataPath))
                dataPath = Path.Combine(app.Environment.ContentRootPath, "scripts", "golf_nz_course.json");
            if (!File.Exists(dataPath))
                throw new FileNotFoundException("Golf NZ data file was not found.", dataPath);

            var sourceVersion = $"sha256:{Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(dataPath))).ToLowerInvariant()}";
            var alreadyImported = await context.GolfNzImportRuns.AnyAsync(run =>
                run.Status == GolfNzImportStatus.Completed && run.SourceVersion == sourceVersion);
            if (!alreadyImported)
            {
                var run = new GolfNzImportRun { StartedAt = DateTime.UtcNow, SourceVersion = sourceVersion };
                context.GolfNzImportRuns.Add(run);
                await context.SaveChangesAsync();
                try
                {
                    var importer = scope.ServiceProvider.GetRequiredService<IGolfNzCourseImporter>();
                    await importer.ImportAsync(run.Id);
                }
                catch (Exception exception)
                {
                    run.Status = GolfNzImportStatus.Failed;
                    run.CompletedAt = DateTime.UtcNow;
                    run.ErrorMessage = exception.Message;
                    await context.SaveChangesAsync();
                    throw;
                }
            }
        }
    }
    finally
    {
        migrationLock.CommandText = "SELECT pg_advisory_unlock(42424201)";
        await migrationLock.ExecuteNonQueryAsync();
        await connection.CloseAsync();
    }
}

app.UseBirdieBuddySecurityHeaders();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<MobileBearerMiddleware>();
app.UseApiObservability();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

app.Run();

public partial class Program { }
