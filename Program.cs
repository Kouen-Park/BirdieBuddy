using Microsoft.EntityFrameworkCore;
using BirdieBuddy.Data;
using BirdieBuddy.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
