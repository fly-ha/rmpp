namespace Rmpp.Infrastructure.Templates;

/// <summary>描述模板包中可定位的结构或完整性错误。</summary>
public sealed record PackageValidationIssue(string Code, string Message, string? EntryPath = null);

/// <summary>模板包无法安全加载或保存时抛出的聚合异常。</summary>
public sealed class RmppPackageException : IOException
{
    public RmppPackageException(string message, IReadOnlyList<PackageValidationIssue>? issues = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Issues = issues ?? Array.Empty<PackageValidationIssue>();
    }

    public IReadOnlyList<PackageValidationIssue> Issues { get; }
}
