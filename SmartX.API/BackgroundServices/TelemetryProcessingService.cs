using SmartX.API.Services;

namespace SmartX.API.BackgroundServices;

/// <summary>
/// Drains the two telemetry queues. The priority queue is always served first, so a
/// critical alert never waits behind routine readings. Packets are taken in small
/// bursts so a flood of traffic cannot hold the loop for long.
/// </summary>
public class TelemetryProcessingService : BackgroundService
{
    private const int MaxPerBurst = 50;

    private readonly ITelemetryQueueService _queue;
    private readonly ITelemetryIngestionService _ingestion;
    private readonly ILogger<TelemetryProcessingService> _logger;

    public TelemetryProcessingService(
        ITelemetryQueueService queue,
        ITelemetryIngestionService ingestion,
        ILogger<TelemetryProcessingService> logger)
    {
        _queue = queue;
        _ingestion = ingestion;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int processed = 0;

            while (processed < MaxPerBurst && _queue.TryDequeue(out var packet, out var wasPriority) && packet is not null)
            {
                try
                {
                    await _ingestion.ProcessPacketAsync(packet, wasPriority);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process a telemetry packet.");
                }

                processed++;
            }

            if (processed == 0)
            {
                await Task.Delay(50, stoppingToken);
            }
        }
    }
}
