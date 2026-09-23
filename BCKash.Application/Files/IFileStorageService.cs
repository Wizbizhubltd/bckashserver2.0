namespace BCKash.Application.Files;

/// <summary>Where a saved file ended up and how big it is. <see cref="Location"/> is an opaque, provider-specific key — callers persist it (e.g. in <c>Document.Location</c>) and pass it back to <see cref="IFileStorageService.ReadAsync"/>/<see cref="IFileStorageService.DeleteAsync"/> unchanged.</summary>
public record StoredFile(string Location, long SizeBytes);

public record StoredFileContent(Stream Content, string ContentType);

/// <summary>
/// Abstracted file storage (FRD §1.1: "local disk for dev, blob storage for production —
/// [VALIDATE WITH BUSINESS] on target hosting"). Used for client documents/identification
/// attachments in Phase 2; later phases (collateral photos, asset files, payroll/expense
/// attachments) reuse the same interface. A blob-storage implementation can be added behind
/// this interface with zero changes to any caller.
/// </summary>
public interface IFileStorageService
{
    /// <summary><paramref name="suggestedFileName"/> is used only to preserve the file extension for later content-type inference — it is not necessarily the name under which the file is stored.</summary>
    Task<StoredFile> SaveAsync(Stream content, string suggestedFileName, CancellationToken cancellationToken = default);

    /// <summary>Null if <paramref name="location"/> doesn't resolve to an existing file.</summary>
    Task<StoredFileContent?> ReadAsync(string location, string originalFileName, CancellationToken cancellationToken = default);

    /// <summary>Best-effort — never throws if the file is already gone.</summary>
    Task DeleteAsync(string location, CancellationToken cancellationToken = default);
}
