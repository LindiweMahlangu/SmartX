using Microsoft.AspNetCore.StaticFiles;
using SmartX.Shared.Models;

namespace SmartX.API.Services;

public class FileStorageService : IFileStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".txt", ".log", ".json", ".cfg", ".conf", ".csv", ".xml", ".png", ".jpg", ".jpeg", ".pdf" };

    private readonly string _rootPath;
    private readonly FileExtensionContentTypeProvider _contentTypes = new();

    public FileStorageService(IWebHostEnvironment env)
    {
        _rootPath = Path.Combine(env.ContentRootPath, "SensorAttachments");
        Directory.CreateDirectory(_rootPath);
    }

    public long MaxFileBytes => 20_000_000;

    public string? Validate(IFormFile file)
    {
        if (file.Length == 0) return "The file is empty.";
        if (file.Length > MaxFileBytes) return $"The file is larger than {MaxFileBytes / 1_000_000} MB.";

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            return $"Files of type \"{extension}\" are not accepted. Allowed types: {string.Join(", ", AllowedExtensions.Order())}.";

        return null;
    }

    public async Task<string> SaveAsync(Guid sensorId, IFormFile file)
    {
        var original = Path.GetFileName(file.FileName);
        foreach (var bad in Path.GetInvalidFileNameChars())
            original = original.Replace(bad, '_');

        var storedName = $"{sensorId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{original}";
        await using var stream = File.Create(Path.Combine(_rootPath, storedName));
        await file.CopyToAsync(stream);
        return storedName;
    }

    public IReadOnlyList<AttachmentInfo> List(Guid sensorId) =>
        Directory.EnumerateFiles(_rootPath, $"{sensorId}_*")
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Select(info => new AttachmentInfo(info.Name, DisplayNameOf(info.Name), info.Length, info.LastWriteTimeUtc))
            .ToList();

    public (Stream Stream, string ContentType, string DisplayName)? Open(Guid sensorId, string storedFileName)
    {
        // Path traversal guard: only a bare file name that belongs to this sensor is accepted.
        if (storedFileName != Path.GetFileName(storedFileName)) return null;
        if (!storedFileName.StartsWith($"{sensorId}_", StringComparison.OrdinalIgnoreCase)) return null;

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storedFileName));
        if (!fullPath.StartsWith(Path.GetFullPath(_rootPath), StringComparison.OrdinalIgnoreCase)) return null;
        if (!File.Exists(fullPath)) return null;

        if (!_contentTypes.TryGetContentType(fullPath, out var contentType))
            contentType = "application/octet-stream";

        return (new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read), contentType, DisplayNameOf(storedFileName));
    }

    private static string DisplayNameOf(string storedName)
    {
        var parts = storedName.Split('_', 3);
        return parts.Length == 3 ? parts[2] : storedName;
    }
}
