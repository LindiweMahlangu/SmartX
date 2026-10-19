using System.Collections.Concurrent;
using SmartX.Shared.Models;

namespace SmartX.API.Services;

public class DeviceStateService : IDeviceStateService
{
    private const int MaxHistoryPerDevice = 25;

    private readonly ConcurrentDictionary<Guid, DeviceState> _states = new();
    private readonly ConcurrentDictionary<Guid, Queue<CommandLogEntry>> _history = new();
    private readonly object _commandLock = new();

    public void UpsertFromTelemetry(TelemetryPacket<float> packet, SensorProfile? sensor)
    {
        _states.AddOrUpdate(
            packet.SensorId,
            _ => new DeviceState
            {
                SensorId = packet.SensorId,
                MacAddress = sensor?.MacAddress ?? "unknown",
                Category = sensor?.Category ?? SensorCategory.Environmental,
                LastMetricName = packet.MetricName,
                LastValue = packet.Value,
                LastUnit = packet.Unit,
                LastStatus = packet.Status,
                LastSeen = packet.Timestamp
            },
            (_, existing) =>
            {
                existing.LastStatus = packet.Status;

                // A disconnect message carries no real reading, so keep the last good one.
                if (packet.Status != AnomalyStatus.Disconnected)
                {
                    existing.LastMetricName = packet.MetricName;
                    existing.LastValue = packet.Value;
                    existing.LastUnit = packet.Unit;
                    existing.LastSeen = packet.Timestamp;
                }

                return existing;
            });
    }

    public IEnumerable<DeviceState> GetAll() => _states.Values.OrderBy(d => d.MacAddress);

    public IEnumerable<DeviceState> Search(string? text, SensorCategory? category, AnomalyStatus? alertState)
    {
        var query = _states.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(text))
        {
            var term = text.Trim();
            query = query.Where(d =>
                d.MacAddress.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                d.LastMetricName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                d.Category.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (category.HasValue) query = query.Where(d => d.Category == category.Value);
        if (alertState.HasValue) query = query.Where(d => d.LastStatus == alertState.Value);

        return query.OrderBy(d => d.MacAddress);
    }

    public CommandOutcome ApplyCommand(DeviceCommandRequest command)
    {
        lock (_commandLock)
        {
            if (command.CommandType == DeviceCommandType.ShutdownAllValves)
            {
                var valves = _states.Values.Where(d => d.Category == SensorCategory.Actuator).ToList();
                if (valves.Count == 0)
                {
                    return new CommandOutcome(false, "No actuator devices are online.", new Dictionary<Guid, bool>());
                }

                // One snapshot of every valve, so a single Undo can restore them all.
                var snapshot = valves.ToDictionary(v => v.SensorId, v => v.ActuatorOn);
                foreach (var valve in valves)
                {
                    valve.ActuatorOn = false;
                }

                return new CommandOutcome(true, $"Shut down {valves.Count} valve(s).", snapshot);
            }

            if (!_states.TryGetValue(command.SensorId, out var state))
            {
                return new CommandOutcome(false, "Device not found.", new Dictionary<Guid, bool>());
            }

            if (state.Category != SensorCategory.Actuator)
            {
                return new CommandOutcome(false, "Override commands only apply to actuator devices.", new Dictionary<Guid, bool>());
            }

            if (state.LastStatus == AnomalyStatus.Disconnected)
            {
                return new CommandOutcome(false, $"{state.MacAddress} is offline.", new Dictionary<Guid, bool>());
            }

            var previous = new Dictionary<Guid, bool> { [state.SensorId] = state.ActuatorOn };

            state.ActuatorOn = command.CommandType switch
            {
                DeviceCommandType.TriggerPump or DeviceCommandType.OpenValve => true,
                DeviceCommandType.StopPump or DeviceCommandType.CloseValve => false,
                DeviceCommandType.ToggleRelay => !state.ActuatorOn,
                _ => state.ActuatorOn
            };

            return new CommandOutcome(true, $"{command.CommandType} executed on {state.MacAddress}.", previous);
        }
    }

    public void RestoreActuatorStates(Dictionary<Guid, bool> snapshot)
    {
        lock (_commandLock)
        {
            foreach (var entry in snapshot)
            {
                if (_states.TryGetValue(entry.Key, out var state))
                {
                    state.ActuatorOn = entry.Value;
                }
            }
        }
    }

    public void RecordCommandResult(DeviceCommandResult result)
    {
        var log = _history.GetOrAdd(result.SensorId, _ => new Queue<CommandLogEntry>());
        lock (log)
        {
            log.Enqueue(new CommandLogEntry(result.CommandType, result.Success, result.Message, result.ExecutedAt));
            while (log.Count > MaxHistoryPerDevice)
            {
                log.Dequeue();
            }
        }
    }

    public IEnumerable<CommandLogEntry> GetHistory(Guid sensorId)
    {
        if (!_history.TryGetValue(sensorId, out var log))
        {
            return Enumerable.Empty<CommandLogEntry>();
        }

        lock (log)
        {
            return log.Reverse().ToList();
        }
    }
}