using SmartX.Shared.Models;
using System.Collections.Concurrent;

namespace SmartX.API.Services
{
    public class InMemorySensorRepository : ISensorRepository
    {
        private readonly ConcurrentDictionary<Guid, SensorProfile> _sensors = new();

        public SensorProfile Add(SensorProfile profile)
        {
            _sensors[profile.Id] = profile;
            return profile;
        }

        public SensorProfile? Get(Guid id) =>
            _sensors.TryGetValue(id, out var sensor) ? sensor : null;

        public IEnumerable<SensorProfile> GetAll() =>
            _sensors.Values.OrderByDescending(s => s.RegisteredAt);
    }
}
