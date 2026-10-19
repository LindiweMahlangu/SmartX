using SmartX.Shared.Models;

namespace SmartX.API.Services
{
    public interface ICommandQueueService
    {
        void Enqueue(DeviceCommandRequest command);
        bool TryDequeue(out DeviceCommandRequest? command);
    }
}
