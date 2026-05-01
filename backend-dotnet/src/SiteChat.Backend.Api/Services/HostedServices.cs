namespace SiteChat.Backend.Api.Services;

/// <summary>
/// Initializes backend providers during application startup.
/// </summary>
public sealed class StartupHostedService(IMongoInfrastructureRepository repository, IServiceProvider serviceProvider, ILogger<StartupHostedService> logger) : IHostedService
{
    private readonly IMongoInfrastructureRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<StartupHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _repository.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
            using var scope = _serviceProvider.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            await auth.EnsureAdminExistsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Provider startup initialization did not complete");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Hosts the future crawl scheduler port.
/// </summary>
public sealed class CrawlSchedulerHostedService(ILogger<CrawlSchedulerHostedService> logger) : BackgroundService
{
    private readonly ILogger<CrawlSchedulerHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Crawl scheduler hosted service started");
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
        }
    }
}
