using SmartX.Shared.Models;

using SmartX.Shared.Models;

namespace SmartX.API.Services;

public interface ISensorRepository
{
    SensorProfile Add(SensorProfile profile);
    SensorProfile? Get(Guid id);
    SensorProfile? GetByMac(string macAddress);
    IEnumerable<SensorProfile> GetAll();
}
