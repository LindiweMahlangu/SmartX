using SmartX.Shared.Models;

namespace SmartX.API.Services
{
    public interface ISensorRepository
    {
        SensorProfile Add(SensorProfile profile);
        SensorProfile? Get(Guid id);
        IEnumerable<SensorProfile> GetAll();
    }
}
