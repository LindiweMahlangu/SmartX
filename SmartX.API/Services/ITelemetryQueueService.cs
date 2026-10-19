using SmartX.Shared.Models;

namespace SmartX.API.Services;

public interface ITelemetryQueueService
{
    bool Enqueue(TelemetryPacket<float> packet, SensorCategory category);
    bool TryDequeue(out TelemetryPacket<float>? packet, out bool wasPriority);
    QueueStats GetStats();
}
