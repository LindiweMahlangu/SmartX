using SmartX.Shared.Models;
using System.Collections.Concurrent;

namespace SmartX.API.Services;

public class InMemorySensorRepository : ISensorRepository
{
    private readonly Dictionary<string, SensorProfile> _byMac = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, SensorProfile> _byId = new();
    private readonly object _lock = new();

    public SensorProfile Add(SensorProfile profile)
    {
        lock (_lock)
        {
            _byId[profile.Id] = profile;
            _byMac[profile.MacAddress] = profile;
        }
        return profile;
    }

    public SensorProfile? Get(Guid id)
    {
        lock (_lock) { return _byId.TryGetValue(id, out var sensor) ? sensor : null; }
    }

    public SensorProfile? GetByMac(string macAddress)
    {
        lock (_lock) { return _byMac.TryGetValue(macAddress, out var sensor) ? sensor : null; }
    }

    public IEnumerable<SensorProfile> GetAll()
    {
        lock (_lock) { return _byId.Values.OrderByDescending(s => s.RegisteredAt).ToList(); }
    }
}
