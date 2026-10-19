using SmartX.Shared.Models;

namespace SmartX.API.Services;

public interface ITelemetryHistoryService
{
    void Add(TelemetryPacket<float> packet);
    IReadOnlyList<TelemetryPacket<float>> GetTimeline(Guid sensorId, int take);
    IReadOnlyList<float> GetRecentValues(Guid sensorId, int take);
}
