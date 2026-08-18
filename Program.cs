using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Data;
using BirdieBuddy.Services;
using BirdieBuddy.Services.External;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// GolfCourseAPI client - BaseUrl/ApiKey come from appsettings.json or (preferably)
// user-secrets, so the key never ends up committed to source control.
builder.Services.AddHttpClient<IGolfCourseApiClient, GolfCourseApiClient>(client =>
{
    var baseUrl = builder.Configuration["GolfCourseApi:BaseUrl"] ?? "https://api.golfcourseapi.com/";
    client.BaseAddress = new Uri(baseUrl);

    var apiKey = builder.Configuration["GolfCourseApi:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
});

builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IRoundService, RoundService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Apply any pending migrations and seed demo data on startup - convenient for
// a student project, though in a real deployment you would run migrations
// as a separate release step instead of on every app start.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
    DbInitializer.Initialize(context);
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
