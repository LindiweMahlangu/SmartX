using SmartX.Shared.Models;
using System.Collections.Concurrent;


namespace SmartX.API.Services
{
    public class CommandQueueService : ICommandQueueService
    {
        private readonly ConcurrentQueue<DeviceCommandRequest> _queue = new();

        public void Enqueue(DeviceCommandRequest command)=> _queue.Enqueue(command);
        

        public bool TryDequeue(out DeviceCommandRequest? command) => _queue.TryDequeue(out command);
        
    }
}
