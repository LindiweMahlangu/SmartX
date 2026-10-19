using Microsoft.AspNetCore.SignalR;
using SmartX.API.Hubs;
using SmartX.API.Services;
using SmartX.Shared.Models;

namespace SmartX.API.BackgroundServices;

public class CommandProcessingService : BackgroundService
{
    private readonly ICommandQueueService _queue;
    private readonly IDeviceStateService _deviceState;
    private readonly ICommandUndoService _undo;
    private readonly IRecommendationEngine _recommendations;
    private readonly ISensorRepository _sensors;
    private readonly IHubContext<TelemetryHub> _hub;

    public CommandProcessingService(
        ICommandQueueService queue,
        IDeviceStateService deviceState,
        ICommandUndoService undo,
        IRecommendationEngine recommendations,
        ISensorRepository sensors,
        IHubContext<TelemetryHub> hub)
    {
        _queue = queue;
        _deviceState = deviceState;
        _undo = undo;
        _recommendations = recommendations;
        _sensors = sensors;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out var command) && command is not null)
            {
                await Task.Delay(400, stoppingToken); // simulated device latency

                var outcome = _deviceState.ApplyCommand(command);
                var description = Describe(command);

                if (outcome.Success)
                {
                    _undo.Push(new UndoEntry(command.SensorId, command.CommandType, description, outcome.PreviousStates));

                    _recommendations.RecordAction(
                        $"cmd:{command.CommandType}:{command.SensorId}",
                        new ActionTemplate(description, command, null));
                }

                var result = new DeviceCommandResult(
                    command.SensorId, command.CommandType, outcome.Success, outcome.Message, DateTime.UtcNow);

                _deviceState.RecordCommandResult(result);
                await _hub.Clients.All.SendAsync("ReceiveCommandResult", result, stoppingToken);
            }
            else
            {
                await Task.Delay(200, stoppingToken);
            }
        }
    }

    private string Describe(DeviceCommandRequest command)
    {
        var label = command.CommandType switch
        {
            DeviceCommandType.TriggerPump => "Trigger pump",
            DeviceCommandType.StopPump => "Stop pump",
            DeviceCommandType.ToggleRelay => "Toggle relay",
            DeviceCommandType.OpenValve => "Open valve",
            DeviceCommandType.CloseValve => "Close valve",
            DeviceCommandType.ShutdownAllValves => "Shut down all valves",
            _ => command.CommandType.ToString()
        };

        if (command.CommandType == DeviceCommandType.ShutdownAllValves)
        {
            return label;
        }

        var mac = _sensors.Get(command.SensorId)?.MacAddress ?? "unknown device";
        return $"{label} on {mac}";
    }
}
