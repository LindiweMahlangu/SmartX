using System;
using System.Collections.Generic;
using System.Text;

namespace SmartX.Shared.Models;

public enum DeviceCommandType
{
    TriggerPump,
    ToggleRelay,
    OpenValve,
    CloseValve,
    StopPump,
    ShutdownAllValves
}

public record DeviceCommandRequest(Guid SensorId, DeviceCommandType CommandType);

public record DeviceCommandResult(
    Guid SensorId, DeviceCommandType CommandType, bool Success, string Message, DateTime ExecutedAt);

public record CommandLogEntry(DeviceCommandType CommandType, bool Success, string Message, DateTime ExecutedAt);