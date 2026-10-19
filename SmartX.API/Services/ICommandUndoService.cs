using SmartX.Shared.Models;

namespace SmartX.API.Services;

public record UndoEntry(Guid SensorId, DeviceCommandType CommandType, string Description, Dictionary<Guid, bool> PreviousStates);

public interface ICommandUndoService
{
    void Push(UndoEntry entry);
    UndoEntry? Pop();
    UndoEntry? Peek();
    int Count { get; }
}
