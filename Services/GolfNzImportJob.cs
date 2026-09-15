using Microsoft.Extensions.DependencyInjection;
using BirdieBuddy.Data;
using BirdieBuddy.Models;

namespace BirdieBuddy.Services;

public sealed class GolfNzImportJob : IGolfNzImportJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GolfNzImportJob> _logger;
    private readonly object _gate = new();

    private GolfNzImportJobStatus _status =
        new("idle", null, null);

    public GolfNzImportJob(
        IServiceScopeFactory scopeFactory,
        ILogger<GolfNzImportJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool TryStart(out GolfNzImportJobStatus status)
    {
        lock (_gate)
        {
            if (_status.State == "running")
            {
                status = _status;
                return false;
            }

            _status = new GolfNzImportJobStatus("running", null, null);
            status = _status;
        }

        _ = Task.Run(RunAsync);
        return true;
    }

    public GolfNzImportJobStatus GetStatus()
    {
        lock (_gate)
        {
            return _status;
        }
    }

    private async Task RunAsync()
    {
        long? runId = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var run = new GolfNzImportRun { StartedAt = DateTime.UtcNow };
            db.GolfNzImportRuns.Add(run);
            await db.SaveChangesAsync();
            runId = run.Id;
            var importer = scope.ServiceProvider.GetRequiredService<IGolfNzCourseImporter>();
            var result = await importer.ImportAsync(run.Id);

            lock (_gate)
            {
                _status = new GolfNzImportJobStatus("completed", result, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Golf NZ course import failed.");
            if (runId.HasValue)
            {
                try
                {
                    using var failureScope = _scopeFactory.CreateScope();
                    var db = failureScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var run = await db.GolfNzImportRuns.FindAsync(runId.Value);
                    if (run is not null)
                    {
                        run.Status = GolfNzImportStatus.Failed;
                        run.CompletedAt = DateTime.UtcNow;
                        run.ErrorMessage = "Golf NZ import failed. Check the server logs.";
                        await db.SaveChangesAsync();
                    }
                }
                catch (Exception failureEx)
                {
                    _logger.LogError(failureEx, "Could not persist Golf NZ import failure state.");
                }
            }

            lock (_gate)
            {
                _status = new GolfNzImportJobStatus(
                    "failed",
                    null,
                    "Golf NZ import failed. Check the server logs.");
            }
        }
    }
}
