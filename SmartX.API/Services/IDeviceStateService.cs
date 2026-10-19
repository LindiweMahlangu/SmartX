using SmartX.Shared.Models;

namespace SmartX.API.Services;

public record CommandOutcome(bool Success, string Message, Dictionary<Guid, bool> PreviousStates);

public interface IDeviceStateService
{
    void UpsertFromTelemetry(TelemetryPacket<float> packet, SensorProfile? sensor);
    IEnumerable<DeviceState> GetAll();
    IEnumerable<DeviceState> Search(string? text, SensorCategory? category, AnomalyStatus? alertState);
    CommandOutcome ApplyCommand(DeviceCommandRequest command);
    void RestoreActuatorStates(Dictionary<Guid, bool> snapshot);
    void RecordCommandResult(DeviceCommandResult result);
    IEnumerable<CommandLogEntry> GetHistory(Guid sensorId);
}
