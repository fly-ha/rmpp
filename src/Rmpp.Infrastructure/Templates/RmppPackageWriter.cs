using System.IO.Compression;
using System.Text.Json;
using Rmpp.Domain.Documents;

namespace Rmpp.Infrastructure.Templates;

/// <summary>将文档、资源和缩略图写成自包含且可公开解析的RMPP模板包。</summary>
public sealed class RmppPackageWriter
{
    private readonly AssetStore assetStore;
    private readonly JsonSerializerOptions jsonOptions;

    public RmppPackageWriter(JsonSerializerOptions? jsonOptions = null, AssetStore? assetStore = null)
    {
        this.jsonOptions = jsonOptions ?? TemplateJsonOptions.Create();
        this.assetStore = assetStore ?? new AssetStore();
    }

    public async Task WriteAsync(Stream destination, TemplatePackageContent content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(content);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        }

        AssetDescriptor[] descriptors = ValidateAssets(content);
        RmppManifestAsset[] manifestAssets = CreateManifestAssets(descriptors);
        RmppManifest manifest = new()
        {
            DocumentId = content.Document.Id,
            CreatedAt = content.Document.Metadata.CreatedAt,
            ModifiedAt = content.Document.Metadata.ModifiedAt,
            Assets = manifestAssets,
        };
        RmppDocumentEnvelope envelope = new() { Document = content.Document };

        using ZipArchive archive = new(destination, ZipArchiveMode.Create, leaveOpen: true);
        await WriteJsonEntryAsync(archive, "manifest.json", manifest, cancellationToken);
        await WriteJsonEntryAsync(archive, "document.json", envelope, cancellationToken);

        foreach (RmppManifestAsset asset in manifestAssets)
        {
            ZipArchiveEntry entry = archive.CreateEntry(asset.EntryPath, CompressionLevel.Optimal);
            await using Stream output = entry.Open();
            await output.WriteAsync(content.Assets[asset.Id], cancellationToken);
        }

        if (content.PreviewPng is { Length: > 0 })
        {
            assetStore.ValidatePreviewPng(content.PreviewPng);
            ZipArchiveEntry entry = archive.CreateEntry("preview.png", CompressionLevel.Optimal);
            await using Stream output = entry.Open();
            await output.WriteAsync(content.PreviewPng, cancellationToken);
        }
    }

    private async Task WriteJsonEntryAsync<T>(ZipArchive archive, string path, T value, CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        await using Stream output = entry.Open();
        await JsonSerializer.SerializeAsync(output, value, jsonOptions, cancellationToken);
    }

    private AssetDescriptor[] ValidateAssets(TemplatePackageContent content)
    {
        HashSet<Guid> declared = content.Document.Assets.Select(static asset => asset.Id).ToHashSet();
        if (!declared.SetEquals(content.Assets.Keys))
        {
            throw new RmppPackageException("Document asset metadata and supplied asset content do not match.");
        }

        return content.Document.Assets
            .Select(asset => assetStore.Inspect(asset, content.Assets[asset.Id]))
            .ToArray();
    }

    private static RmppManifestAsset[] CreateManifestAssets(IEnumerable<AssetDescriptor> descriptors) =>
        descriptors.Select(static asset => new RmppManifestAsset(
            asset.Id,
            asset.EntryPath,
            asset.FileName,
            asset.MediaType,
            asset.Length,
            asset.Sha256)).ToArray();
}
