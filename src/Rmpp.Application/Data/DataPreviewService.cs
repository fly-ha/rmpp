using System.Globalization;
using Rmpp.Application.Abstractions;
using Rmpp.Application.Data.Expressions;
using Rmpp.Application.Validation;
using Rmpp.Domain.Data;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Rendering.Layout;

namespace Rmpp.Application.Data;

/// <summary>保存一条记录按最终规则解析后的元素值和记录级问题。</summary>
public sealed record ResolvedDataRecord(
    DataRowSnapshot Row,
    IReadOnlyList<ResolvedElement> Elements,
    IReadOnlyList<ValidationIssue> Issues);

/// <summary>提供记录导航、搜索和与最终输出共享的表达式/流水号/时间解析。</summary>
public sealed class DataPreviewService(
    ExpressionParser? parser = null,
    ExpressionEvaluator? evaluator = null,
    IClock? clock = null)
{
    private readonly ExpressionParser parser = parser ?? new ExpressionParser();
    private readonly ExpressionEvaluator evaluator = evaluator ?? new ExpressionEvaluator();
    private readonly IClock clock = clock ?? new SystemClock();

    public static IReadOnlyList<DataRowSnapshot> Range(DataSetSnapshot dataSet, int startIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return dataSet.Rows.Skip(startIndex).Take(count).ToArray();
    }

    public static int FindNext(DataSetSnapshot dataSet, string query, int startIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        for (int index = startIndex; index < dataSet.Rows.Count; index++)
        {
            if (dataSet.Rows[index].Values.Values.Any(value =>
                    value?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true))
            {
                return index;
            }
        }

        return -1;
    }

    public ResolvedDataRecord Resolve(
        TemplateDocument document,
        DataRowSnapshot row,
        JobTimeContext jobTime,
        SerialPlan? serialPlan = null,
        int copyIndex = 0,
        DateTimeOffset? pageTime = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(jobTime);
        ExpressionEvaluationContext expressionContext = new()
        {
            Fields = row.Values,
            Culture = jobTime.Culture,
        };
        List<ResolvedElement> resolved = [];
        List<ValidationIssue> issues = [];
        foreach (TemplateElement element in document.Elements)
        {
            try
            {
                switch (element)
                {
                    case TextElement text:
                        resolved.Add(Text(text.Id, ResolveExpression(text.Content, expressionContext)));
                        break;
                    case BarcodeElement barcode:
                        resolved.Add(Text(barcode.Id, ResolveExpression(barcode.Content, expressionContext)));
                        break;
                    case ImageElement { VariablePath: not null } image:
                        resolved.Add(new ResolvedElement
                        {
                            ElementId = image.Id,
                            LocalImagePath = ResolveExpression(image.VariablePath, expressionContext),
                        });
                        break;
                    case SerialElement serial:
                        PlannedSerialValue? serialValue = serialPlan?.Find(serial.Id, row.Index, copyIndex);
                        string serialText = serialValue?.Text ?? FormatSerial(serial.Definition, row.Index, copyIndex);
                        resolved.Add(Text(serial.Id, serialText));
                        break;
                    case DateTimeElement dateTime:
                        DateTimeOffset value = jobTime.Resolve(dateTime.Definition, pageTime, clock.LocalNow);
                        CultureInfo culture = string.IsNullOrWhiteSpace(dateTime.Definition.CultureName)
                            ? jobTime.Culture
                            : CultureInfo.GetCultureInfo(dateTime.Definition.CultureName);
                        resolved.Add(Text(dateTime.Id, value.ToString(dateTime.Definition.Format, culture)));
                        break;
                }
            }
            catch (Exception exception) when (exception is ExpressionParseException
                                               or ExpressionEvaluationException
                                               or ArgumentException
                                               or FormatException
                                               or OverflowException)
            {
                issues.Add(new ValidationIssue(
                    "data-resolution-error",
                    exception.Message,
                    ValidationSeverity.Error,
                    new ValidationLocation
                    {
                        DocumentId = document.Id,
                        PageNumber = 1,
                        ElementId = element.Id,
                        RecordIndex = row.Index,
                    }));
            }
        }

        return new ResolvedDataRecord(row, resolved, issues);
    }

    private string ResolveExpression(ElementExpression expression, ExpressionEvaluationContext context)
    {
        if (!BindingValidationService.IsDynamic(expression.Source))
        {
            return expression.Source;
        }

        return evaluator.EvaluateText(parser.Parse(expression.Source[1..]), context);
    }

    private static ResolvedElement Text(Guid elementId, string value) =>
        new() { ElementId = elementId, Text = value };

    private static string FormatSerial(SerialDefinition definition, int recordIndex, int copyIndex)
    {
        SerialDefinition validated = definition.Validate();
        long sequenceIndex = validated.AdvancePerCopy
            ? checked((long)recordIndex + copyIndex)
            : recordIndex;
        long numeric = checked(validated.Start + validated.Step * sequenceIndex);
        string number = validated.MinimumDigits > 0
            ? numeric.ToString($"D{validated.MinimumDigits}", CultureInfo.InvariantCulture)
            : numeric.ToString(CultureInfo.InvariantCulture);
        return validated.Prefix + number + validated.Suffix;
    }
}
