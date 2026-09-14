using SmartX.API.Services;

namespace SmartX.Api.Services;

public class FileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public FileStorageService(IWebHostEnvironment env)
    {
        _rootPath = Path.Combine(env.ContentRootPath, "SensorAttachments");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Guid sensorId, IFormFile file)
    {
        var safeFileName = $"{sensorId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Path.GetFileName(file.FileName)}";
        var fullPath = Path.Combine(_rootPath, safeFileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        return safeFileName;
    }
}
