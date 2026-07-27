using Rmpp.Application.Data.Expressions;
using Rmpp.Application.Validation;
using Rmpp.Domain.Data;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

namespace Rmpp.Application.Data;

public sealed record BindingValidationResult(
    IReadOnlySet<string> ReferencedFields,
    IReadOnlySet<string> UnusedFields,
    IReadOnlyList<ValidationIssue> Issues);

/// <summary>解析元素表达式并报告缺失、未使用字段及位置感知语法错误。</summary>
public sealed class BindingValidationService(ExpressionParser? parser = null)
{
    private readonly ExpressionParser parser = parser ?? new ExpressionParser();

    public BindingValidationResult Validate(TemplateDocument document, DataSchema schema)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(schema);
        HashSet<string> available = schema.Columns.Select(static column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> referenced = new(StringComparer.OrdinalIgnoreCase);
        List<ValidationIssue> issues = [];
        foreach ((TemplateElement element, string propertyName, ElementExpression expression) in Expressions(document))
        {
            if (!IsDynamic(expression.Source))
            {
                continue;
            }

            try
            {
                ExpressionNode node = parser.Parse(expression.Source[1..]);
                foreach (FieldNode field in Descendants(node).OfType<FieldNode>())
                {
                    referenced.Add(field.FieldName);
                    if (!available.Contains(field.FieldName))
                    {
                        issues.Add(new ValidationIssue(
                            "missing-data-field",
                            $"Expression references missing field '{field.FieldName}'.",
                            ValidationSeverity.Error,
                            Location(document.Id, element.Id, propertyName)));
                    }
                }
            }
            catch (ExpressionParseException exception)
            {
                issues.Add(new ValidationIssue(
                    "invalid-expression",
                    $"Expression error at {exception.Span.Start}: {exception.Message}",
                    ValidationSeverity.Error,
                    Location(document.Id, element.Id, propertyName)));
            }
        }

        HashSet<string> unused = available.Where(field => !referenced.Contains(field))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        issues.AddRange(unused.Select(field => new ValidationIssue(
            "unused-data-field",
            $"Imported field '{field}' is not referenced by the template.",
            ValidationSeverity.Information,
            new ValidationLocation { DocumentId = document.Id, PropertyPath = field })));
        return new BindingValidationResult(referenced, unused, issues);
    }

    internal static bool IsDynamic(string source) => source.StartsWith('=');

    internal static IEnumerable<(TemplateElement Element, string PropertyName, ElementExpression Expression)> Expressions(
        TemplateDocument document)
    {
        foreach (TemplateElement element in document.Elements)
        {
            switch (element)
            {
                case TextElement text:
                    yield return (text, nameof(text.Content), text.Content);
                    break;
                case BarcodeElement barcode:
                    yield return (barcode, nameof(barcode.Content), barcode.Content);
                    break;
                case ImageElement { VariablePath: not null } image:
                    yield return (image, nameof(image.VariablePath), image.VariablePath);
                    break;
            }
        }
    }

    internal static IEnumerable<ExpressionNode> Descendants(ExpressionNode node)
    {
        yield return node;
        IEnumerable<ExpressionNode> children = node switch
        {
            BinaryNode binary => [binary.Left, binary.Right],
            ConditionalNode conditional => [conditional.Condition, conditional.WhenTrue, conditional.WhenFalse],
            FunctionNode function => function.Arguments,
            _ => Array.Empty<ExpressionNode>(),
        };
        foreach (ExpressionNode child in children)
        {
            foreach (ExpressionNode descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static ValidationLocation Location(Guid documentId, Guid elementId, string propertyName) =>
        new()
        {
            DocumentId = documentId,
            PageNumber = 1,
            ElementId = elementId,
            PropertyPath = propertyName,
        };
}
