using Rmpp.Infrastructure.Persistence.Models;
using Rmpp.Infrastructure.Templates;

namespace Rmpp.Infrastructure.Persistence;

public sealed record TemplateScanIssue(string Path, string Message);
public sealed record TemplateScanResult(int ScannedFiles, int ValidTemplates, IReadOnlyList<TemplateScanIssue> Issues);

/// <summary>从用户选择的目录重建模板元数据和缩略图，不把SQLite视为模板真源。</summary>
public sealed class TemplateCatalogScanner(
    TemplateCatalogRepository repository,
    RmppPackageReader? packageReader = null)
{
    private readonly RmppPackageReader reader = packageReader ?? new RmppPackageReader();

    public async Task<TemplateScanResult> ScanAsync(
        IEnumerable<string> directories,
        CancellationToken cancellationToken = default)
    {
        string[] roots = directories
            .Select(SqlitePersistenceHelpers.NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<TemplateScanIssue> issues = [];
        int scanned = 0;
        int valid = 0;

        foreach (string root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(root))
            {
                issues.Add(new TemplateScanIssue(root, "Directory does not exist."));
                continue;
            }

            foreach (string path in Directory.EnumerateFiles(root, "*.rmpp", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string normalized = SqlitePersistenceHelpers.NormalizePath(path);
                seen.Add(normalized);
                scanned++;
                try
                {
                    await using FileStream stream = new(normalized, FileMode.Open, FileAccess.Read, FileShare.Read);
                    TemplatePackageContent package = await reader.ReadAsync(stream, cancellationToken);
                    FileInfo file = new(normalized);
                    await repository.UpsertAsync(new TemplateCatalogEntry
                    {
                        DocumentId = package.Document.Id,
                        Path = normalized,
                        Title = package.Document.Metadata.Title,
                        Description = package.Document.Metadata.Description,
                        FileModifiedAt = file.LastWriteTimeUtc,
                        DocumentModifiedAt = package.Document.Metadata.ModifiedAt,
                        LastScannedAt = DateTimeOffset.UtcNow,
                        Status = TemplateCatalogStatus.Available,
                        ThumbnailPng = package.PreviewPng,
                    }, cancellationToken);
                    valid++;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    issues.Add(new TemplateScanIssue(normalized, exception.Message));
                }
            }
        }

        await repository.ReconcileScannedRootsAsync(roots, seen, cancellationToken);
        return new TemplateScanResult(scanned, valid, issues);
    }
}
