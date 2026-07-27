namespace Rmpp.Infrastructure.Templates;

/// <summary>在目标目录创建临时包，校验成功后再替换正式模板，避免半写文件。</summary>
public sealed class AtomicTemplateFileWriter
{
    private readonly RmppPackageWriter packageWriter;
    private readonly RmppPackageValidator packageValidator;

    public AtomicTemplateFileWriter(RmppPackageWriter? packageWriter = null, RmppPackageValidator? packageValidator = null)
    {
        this.packageWriter = packageWriter ?? new RmppPackageWriter();
        this.packageValidator = packageValidator ?? new RmppPackageValidator();
    }

    public async Task WriteAsync(string destinationPath, TemplatePackageContent content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        string fullPath = Path.GetFullPath(destinationPath);
        string directory = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("Destination must have a parent directory.", nameof(destinationPath));
        Directory.CreateDirectory(directory);

        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        string backupPath = fullPath + ".bak";
        try
        {
            await using (FileStream output = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await packageWriter.WriteAsync(output, content, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            await using (FileStream validation = new(temporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous))
            {
                await packageValidator.ValidateAsync(validation, cancellationToken);
            }

            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, backupPath, ignoreMetadataErrors: true);
                File.Delete(backupPath);
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
