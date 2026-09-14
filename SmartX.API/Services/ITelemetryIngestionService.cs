using SmartX.Shared.Models;

namespace SmartX.API.Services;

public interface ITelemetryIngestionService
{
    Task<List<TelemetryPacket<float>>> IngestBatchesAsync(TelemetryBatchRequest request);
    List<TelemetryPacket<float>> GetHistory(Guid sensorId);
}
