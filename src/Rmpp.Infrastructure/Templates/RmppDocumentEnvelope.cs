using Rmpp.Domain.Documents;

namespace Rmpp.Infrastructure.Templates;

/// <summary>为模板正文增加独立模式版本，便于未来逐版本迁移。</summary>
public sealed record RmppDocumentEnvelope
{
    public int SchemaVersion { get; init; } = TemplateFormatVersion.CurrentSchemaVersion;
    public string MinimumAppVersion { get; init; } = TemplateFormatVersion.CurrentApplicationVersion;
    public required TemplateDocument Document { get; init; }
}
