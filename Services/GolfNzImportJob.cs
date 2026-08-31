using Microsoft.Extensions.DependencyInjection;

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
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var importer = scope.ServiceProvider.GetRequiredService<IGolfNzCourseImporter>();
            var result = await importer.ImportAsync();

            lock (_gate)
            {
                _status = new GolfNzImportJobStatus("completed", result, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Golf NZ course import failed.");

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
