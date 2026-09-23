using BCKash.Application.Files;
using Microsoft.Extensions.Options;

namespace BCKash.Infrastructure.Files;

/// <summary>
/// Dev/default <see cref="IFileStorageService"/> implementation — writes to a local directory.
/// FRD §1.1 calls for a blob-storage implementation in production once hosting is confirmed
/// [VALIDATE WITH BUSINESS]; that can be added behind the same interface with zero changes
/// to any caller (e.g. DocumentsController).
/// </summary>
public class LocalDiskFileStorageService : IFileStorageService
{
    // Content type is never stored (neither Document nor the legacy schema has a
    // content-type column) — it's re-derived from the filename extension at download time.
    // Small, fixed map rather than Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider,
    // which lives in the ASP.NET Core shared framework and isn't available to this plain
    // class-library project; this is sufficient for the document types Phase 2 needs to round-trip.
    private static readonly Dictionary<string, string> ContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".webp"] = "image/webp",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".csv"] = "text/csv",
        [".txt"] = "text/plain",
    };

    private const string DefaultContentType = "application/octet-stream";

    private readonly string _rootPath;

    public LocalDiskFileStorageService(IOptions<FileStorageSettings> settings)
    {
        var configuredPath = settings.Value.RootPath;
        _rootPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);
    }

    public async Task<StoredFile> SaveAsync(Stream content, string suggestedFileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(suggestedFileName);
        var relativePath = Path.Combine(
            DateTime.UtcNow.ToString("yyyy"),
            DateTime.UtcNow.ToString("MM"),
            $"{Guid.NewGuid():N}{extension}");

        var absolutePath = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using (var fileStream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        var sizeBytes = new FileInfo(absolutePath).Length;
        return new StoredFile(relativePath.Replace(Path.DirectorySeparatorChar, '/'), sizeBytes);
    }

    public Task<StoredFileContent?> ReadAsync(string location, string originalFileName, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolvePath(location);
        if (!File.Exists(absolutePath))
        {
            return Task.FromResult<StoredFileContent?>(null);
        }

        var extension = Path.GetExtension(originalFileName);
        var contentType = extension is not null && ContentTypesByExtension.TryGetValue(extension, out var mapped)
            ? mapped
            : DefaultContentType;

        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult<StoredFileContent?>(new StoredFileContent(stream, contentType));
    }

    public Task DeleteAsync(string location, CancellationToken cancellationToken = default)
    {
        try
        {
            File.Delete(ResolvePath(location));
        }
        catch (DirectoryNotFoundException)
        {
            // Already gone — best-effort delete, never throws.
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string location) => Path.Combine(_rootPath, location.Replace('/', Path.DirectorySeparatorChar));
}
