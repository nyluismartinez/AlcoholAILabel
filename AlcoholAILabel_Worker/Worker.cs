using AlcoholAILabel.Worker.Services;
using AlcoholAILabel_Worker.Services;

namespace AlcoholAILabel_Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
 

    public Worker(ILogger<Worker> logger, IConfiguration configuration, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //while (!stoppingToken.IsCancellationRequested)
        //{
        //    if (_logger.IsEnabled(LogLevel.Information))
        //    {
        //        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
        //    }
        //    await Task.Delay(1000, stoppingToken);
        //}


        var pollingSeconds = _configuration.GetValue<int>("WorkerSettings:PollingSeconds", 5);

        _logger.LogInformation("Alcohol label worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var processor = scope.ServiceProvider
                    .GetRequiredService<AlcoholLabelJobProcessor>();

                await processor.ProcessNextJobAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(pollingSeconds), stoppingToken);
        }


    }
}
