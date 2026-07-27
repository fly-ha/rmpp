namespace Rmpp.Infrastructure.Templates;

/// <summary>通过完整读取验证模板包，供原子保存和外部检查复用。</summary>
public sealed class RmppPackageValidator
{
    private readonly RmppPackageReader reader;

    public RmppPackageValidator(RmppPackageReader? reader = null)
    {
        this.reader = reader ?? new RmppPackageReader();
    }

    public async Task ValidateAsync(Stream source, CancellationToken cancellationToken = default)
    {
        _ = await reader.ReadAsync(source, cancellationToken);
    }
}
