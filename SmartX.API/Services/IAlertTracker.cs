using SmartX.Shared.Models;

namespace SmartX.API.Services;

public interface IAlertTracker
{
    AlertEvent? Evaluate(TelemetryPacket<float> packet, string macAddress, string condition);
    AlertSnapshot GetSnapshot();
}
