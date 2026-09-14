namespace SmartX.API.Services
{
    public interface IFileStorageService
    {
        Task<string>SaveAsync(Guid SensorId, IFormFile file);
    }
}
