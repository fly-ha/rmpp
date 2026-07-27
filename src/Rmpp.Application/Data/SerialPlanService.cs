using System.Globalization;
using Rmpp.Domain.Data;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

namespace Rmpp.Application.Data;

/// <summary>保存一个元素在特定记录和副本上的不可变流水号。</summary>
public sealed record PlannedSerialValue(
    Guid ElementId,
    int RecordIndex,
    int CopyIndex,
    long NumericValue,
    string Text);

public sealed record SerialPlan(IReadOnlyList<PlannedSerialValue> Values)
{
    public PlannedSerialValue? Find(Guid elementId, int recordIndex, int copyIndex) =>
        Values.FirstOrDefault(value =>
            value.ElementId == elementId
            && value.RecordIndex == recordIndex
            && value.CopyIndex == copyIndex);
}

/// <summary>在输出前按记录/副本策略计算全部流水号，后续预览和打印只读取计划。</summary>
public sealed class SerialPlanService
{
    public static SerialPlan Create(TemplateDocument document, int recordCount, int copiesPerRecord = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recordCount);
        return Create(document, Enumerable.Range(0, recordCount).ToArray(), copiesPerRecord);
    }

    /// <summary>按本次任务的记录顺序推进流水号，同时保留源记录索引用于查找。</summary>
    public static SerialPlan Create(
        TemplateDocument document,
        IReadOnlyList<int> recordIndices,
        int copiesPerRecord = 1)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(recordIndices);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(copiesPerRecord);
        if (recordIndices.Any(static index => index < 0) || recordIndices.Distinct().Count() != recordIndices.Count)
        {
            throw new ArgumentException("Record indices must be non-negative and unique.", nameof(recordIndices));
        }

        List<PlannedSerialValue> values = [];
        foreach (SerialElement element in document.Elements.OfType<SerialElement>())
        {
            SerialDefinition definition = element.Definition.Validate();
            for (int sequenceRecordIndex = 0; sequenceRecordIndex < recordIndices.Count; sequenceRecordIndex++)
            {
                int recordIndex = recordIndices[sequenceRecordIndex];
                for (int copyIndex = 0; copyIndex < copiesPerRecord; copyIndex++)
                {
                    long sequenceIndex = definition.AdvancePerCopy
                        ? checked((long)sequenceRecordIndex * copiesPerRecord + copyIndex)
                        : sequenceRecordIndex;
                    long numeric = checked(definition.Start + definition.Step * sequenceIndex);
                    string number = definition.MinimumDigits > 0
                        ? numeric.ToString($"D{definition.MinimumDigits}", CultureInfo.InvariantCulture)
                        : numeric.ToString(CultureInfo.InvariantCulture);
                    values.Add(new PlannedSerialValue(
                        element.Id,
                        recordIndex,
                        copyIndex,
                        numeric,
                        definition.Prefix + number + definition.Suffix));
                }
            }
        }

        return new SerialPlan(values);
    }
}
