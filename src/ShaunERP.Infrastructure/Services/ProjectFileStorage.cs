namespace ShaunERP.Infrastructure.Services;

/// <summary>
/// Physical storage for project attachments. Files never keep their original name on disk;
/// a UUID is used so uploads cannot collide or be guessed, and the stored path is always
/// relative to the configured root so it can never escape the storage directory.
/// </summary>
public interface IProjectFileStorage
{
    long MaxFileSizeBytes { get; }
    IReadOnlyCollection<string> AllowedExtensions { get; }

    /// <summary>Validates name/size and throws <see cref="InvalidOperationException"/> when rejected.</summary>
    void Validate(string fileName, long sizeBytes);

    /// <summary>Persists the payload and returns the relative storage path to record on the entity.</summary>
    Task<string> SaveAsync(int companyId, string fileName, byte[] content, CancellationToken ct = default);

    Task<byte[]?> ReadAsync(string storagePath, CancellationToken ct = default);

    void Delete(string storagePath);
}

public sealed class ProjectFileStorage : IProjectFileStorage
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".docx", ".xlsx", ".txt", ".csv", ".zip"
    };

    private readonly string _root;

    public ProjectFileStorage(string rootPath) => _root = rootPath;

    public long MaxFileSizeBytes => 10L * 1024 * 1024;

    public IReadOnlyCollection<string> AllowedExtensions => Allowed;

    public void Validate(string fileName, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidOperationException("A file name is required.");
        if (sizeBytes <= 0)
            throw new InvalidOperationException("The file is empty.");
        if (sizeBytes > MaxFileSizeBytes)
            throw new InvalidOperationException($"Files must be {MaxFileSizeBytes / (1024 * 1024)} MB or smaller.");

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !Allowed.Contains(extension))
            throw new InvalidOperationException($"File type '{extension}' is not allowed. Allowed: {string.Join(", ", Allowed)}");
    }

    public async Task<string> SaveAsync(int companyId, string fileName, byte[] content, CancellationToken ct = default)
    {
        Validate(fileName, content.LongLength);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var relative = Path.Combine(companyId.ToString(), $"{Guid.NewGuid():N}{extension}");
        var absolute = ResolveAbsolute(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllBytesAsync(absolute, content, ct);
        return relative.Replace('\\', '/');
    }

    public async Task<byte[]?> ReadAsync(string storagePath, CancellationToken ct = default)
    {
        var absolute = ResolveAbsolute(storagePath);
        return File.Exists(absolute) ? await File.ReadAllBytesAsync(absolute, ct) : null;
    }

    public void Delete(string storagePath)
    {
        var absolute = ResolveAbsolute(storagePath);
        if (File.Exists(absolute))
        {
            File.Delete(absolute);
        }
    }

    private string ResolveAbsolute(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new InvalidOperationException("Attachment storage path is missing.");

        var rootFull = Path.GetFullPath(_root);
        var combined = Path.GetFullPath(Path.Combine(rootFull, storagePath));
        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid attachment path.");
        return combined;
    }
}
