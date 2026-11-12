using SmartX.Shared.Models;

namespace SmartX.API.Services;

public interface IFileStorageService
{
    long MaxFileBytes { get; }
    string? Validate(IFormFile file);
    Task<string> SaveAsync(Guid sensorId, IFormFile file);
    IReadOnlyList<AttachmentInfo> List(Guid sensorId);
    (Stream Stream, string ContentType, string DisplayName)? Open(Guid sensorId, string storedFileName);
}
