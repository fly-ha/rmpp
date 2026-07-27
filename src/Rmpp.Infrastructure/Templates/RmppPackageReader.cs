using System.IO.Compression;
using System.Text.Json;
using Rmpp.Domain.Documents;

namespace Rmpp.Infrastructure.Templates;

/// <summary>把模板包视为不可信输入，并在返回领域文档前完成结构、版本和哈希校验。</summary>
public sealed class RmppPackageReader
{
    private readonly AssetStore assetStore;
    private readonly PackageLimits limits;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly SchemaMigrationRegistry migrations;

    public RmppPackageReader(PackageLimits? limits = null, JsonSerializerOptions? jsonOptions = null, SchemaMigrationRegistry? migrations = null)
    {
        this.limits = limits ?? new PackageLimits();
        this.jsonOptions = jsonOptions ?? TemplateJsonOptions.Create();
        this.migrations = migrations ?? new SchemaMigrationRegistry();
        assetStore = new AssetStore(this.limits);
    }

    public async Task<TemplatePackageContent> ReadAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        using ZipArchive archive = new(source, ZipArchiveMode.Read, leaveOpen: true);
        Dictionary<string, ZipArchiveEntry> entries = ValidateStructure(archive);

        RmppManifest manifest = await ReadJsonAsync<RmppManifest>(entries["manifest.json"], cancellationToken);
        ValidateManifest(manifest);
        RmppDocumentEnvelope envelope = await ReadDocumentEnvelopeAsync(
            entries["document.json"],
            manifest.SchemaVersion,
            manifest.MinimumAppVersion,
            cancellationToken);
        if (manifest.DocumentId != envelope.Document.Id)
        {
            throw new RmppPackageException("Manifest document id does not match document.json.");
        }

        TemplateDocument document = migrations.Migrate(envelope.Document, envelope.SchemaVersion, TemplateFormatVersion.CurrentSchemaVersion);
        Dictionary<Guid, byte[]> assets = await ReadAssetsAsync(entries, manifest, document, cancellationToken);
        byte[]? preview = entries.TryGetValue("preview.png", out ZipArchiveEntry? previewEntry)
            ? await ReadEntryAsync(previewEntry, limits.MaximumAssetBytes, cancellationToken)
            : null;
        if (preview is not null)
        {
            assetStore.ValidatePreviewPng(preview);
        }

        return new TemplatePackageContent { Document = document, Assets = assets, PreviewPng = preview };
    }

    private Dictionary<string, ZipArchiveEntry> ValidateStructure(ZipArchive archive)
    {
        if (archive.Entries.Count > limits.MaximumEntries)
        {
            throw new RmppPackageException("Template package contains too many entries.");
        }

        long total = 0;
        Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.OrdinalIgnoreCase);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string path = entry.FullName.Replace('\\', '/');
            if (Path.IsPathRooted(path) || path.Split('/').Any(static part => part == ".."))
            {
                throw new RmppPackageException("Template package contains an unsafe entry path.", [new("unsafe-path", "Entry path escapes package boundary.", path)]);
            }

            if (!entries.TryAdd(path, entry))
            {
                throw new RmppPackageException($"Template package contains duplicate entry: {path}");
            }

            total = checked(total + entry.Length);
            if (total > limits.MaximumTotalUncompressedBytes)
            {
                throw new RmppPackageException("Template package is too large after decompression.");
            }
        }

        if (!entries.ContainsKey("manifest.json") || !entries.ContainsKey("document.json"))
        {
            throw new RmppPackageException("Template package must contain manifest.json and document.json.");
        }

        return entries;
    }

    private static void ValidateManifest(RmppManifest manifest)
    {
        if (!string.Equals(manifest.Format, TemplateFormatVersion.FormatIdentifier, StringComparison.Ordinal))
        {
            throw new RmppPackageException("File is not an RMPP template package.");
        }

        if (manifest.SchemaVersion <= 0 || manifest.SchemaVersion > TemplateFormatVersion.CurrentSchemaVersion)
        {
            throw new RmppPackageException($"Unsupported manifest schema version: {manifest.SchemaVersion}.");
        }

        ValidateMinimumAppVersion(manifest.MinimumAppVersion, "manifest.json");
    }

    private async Task<Dictionary<Guid, byte[]>> ReadAssetsAsync(
        Dictionary<string, ZipArchiveEntry> entries,
        RmppManifest manifest,
        TemplateDocument document,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, AssetReference> documentAssets = document.Assets.ToDictionary(static asset => asset.Id);
        if (manifest.Assets.Select(static asset => asset.Id).Distinct().Count() != manifest.Assets.Count ||
            manifest.Assets.Select(static asset => asset.EntryPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Assets.Count ||
            !documentAssets.Keys.ToHashSet().SetEquals(manifest.Assets.Select(static asset => asset.Id)))
        {
            throw new RmppPackageException("Manifest and document asset lists do not match.");
        }

        HashSet<string> declaredEntryPaths = manifest.Assets
            .Select(static asset => asset.EntryPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (entries.Keys.Any(path => path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) && !declaredEntryPaths.Contains(path)))
        {
            throw new RmppPackageException("Template package contains an undeclared asset entry.");
        }

        Dictionary<Guid, byte[]> result = [];
        foreach (RmppManifestAsset asset in manifest.Assets)
        {
            if (!entries.TryGetValue(asset.EntryPath, out ZipArchiveEntry? entry))
            {
                throw new RmppPackageException($"Missing asset entry: {asset.EntryPath}");
            }

            byte[] bytes = await ReadEntryAsync(entry, limits.MaximumAssetBytes, cancellationToken);
            AssetReference reference = documentAssets[asset.Id];
            assetStore.Validate(asset, reference, bytes);

            result.Add(asset.Id, bytes);
        }

        return result;
    }

    private async Task<T> ReadJsonAsync<T>(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        byte[] bytes = await ReadEntryAsync(entry, limits.MaximumJsonBytes, cancellationToken);
        return DeserializeJson<T>(bytes, entry.FullName);
    }

    private async Task<RmppDocumentEnvelope> ReadDocumentEnvelopeAsync(
        ZipArchiveEntry entry,
        int manifestSchemaVersion,
        string manifestMinimumAppVersion,
        CancellationToken cancellationToken)
    {
        byte[] bytes = await ReadEntryAsync(entry, limits.MaximumJsonBytes, cancellationToken);
        try
        {
            using JsonDocument json = JsonDocument.Parse(bytes);
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("schemaVersion", out JsonElement schemaElement) ||
                !schemaElement.TryGetInt32(out int schemaVersion))
            {
                throw new RmppPackageException("document.json must declare an integer schemaVersion.");
            }

            if (schemaVersion <= 0 || schemaVersion > TemplateFormatVersion.CurrentSchemaVersion)
            {
                throw new RmppPackageException($"Template schema {schemaVersion} requires a newer application.");
            }

            if (schemaVersion != manifestSchemaVersion)
            {
                throw new RmppPackageException("Manifest and document schema versions do not match.");
            }

            if (!json.RootElement.TryGetProperty("minimumAppVersion", out JsonElement appVersionElement) ||
                appVersionElement.ValueKind != JsonValueKind.String ||
                appVersionElement.GetString() is not { } minimumAppVersion)
            {
                throw new RmppPackageException("document.json must declare minimumAppVersion.");
            }

            ValidateMinimumAppVersion(minimumAppVersion, "document.json");
            if (!string.Equals(minimumAppVersion, manifestMinimumAppVersion, StringComparison.Ordinal))
            {
                throw new RmppPackageException("Manifest and document minimum application versions do not match.");
            }
        }
        catch (JsonException exception)
        {
            throw new RmppPackageException($"Invalid JSON in {entry.FullName}.", innerException: exception);
        }

        return DeserializeJson<RmppDocumentEnvelope>(bytes, entry.FullName);
    }

    private static void ValidateMinimumAppVersion(string value, string entryPath)
    {
        if (!Version.TryParse(value, out Version? requiredVersion) ||
            !Version.TryParse(TemplateFormatVersion.CurrentApplicationVersion, out Version? currentVersion))
        {
            throw new RmppPackageException($"Invalid minimum application version in {entryPath}.");
        }

        if (requiredVersion > currentVersion)
        {
            throw new RmppPackageException(
                $"Template requires application version {requiredVersion} or newer.");
        }
    }

    private T DeserializeJson<T>(byte[] bytes, string entryPath)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(bytes, jsonOptions)
                ?? throw new RmppPackageException($"Entry is empty or invalid: {entryPath}");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new RmppPackageException($"Invalid JSON in {entryPath}.", innerException: exception);
        }
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry, long maximumBytes, CancellationToken cancellationToken)
    {
        if (entry.Length > maximumBytes || entry.Length > int.MaxValue)
        {
            throw new RmppPackageException($"Entry exceeds size limit: {entry.FullName}");
        }

        byte[] bytes = new byte[checked((int)entry.Length)];
        await using Stream stream = entry.Open();
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }
}
