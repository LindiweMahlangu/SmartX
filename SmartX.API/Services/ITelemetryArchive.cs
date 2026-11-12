using SmartX.Shared.Models;

namespace SmartX.API.Services;

public interface ITelemetryArchive
{ 
    void Add(TelemetryPacket<float> packet);
    void Seed(Guid sensorId, IEnumerable<TelemetryPacket<float>> packets);

    HistoryQueryResult Query(
        Guid sensorId, string macAddress, DateTime fromUtc, DateTime toUtc, int limit, bool newestFirst);
}
