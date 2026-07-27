using Rmpp.Domain.Documents;

namespace Rmpp.Infrastructure.Templates;

/// <summary>定义相邻模板正文版本之间的纯内存迁移。</summary>
public interface ITemplateSchemaMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    TemplateDocument Migrate(TemplateDocument document);
}
