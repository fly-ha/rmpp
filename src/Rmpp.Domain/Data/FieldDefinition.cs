using Rmpp.Domain.Common;

namespace Rmpp.Domain.Data;

public enum FieldDataType { Text, Number, DateTime, Boolean, ImagePath }

public sealed record FieldDefinition
{
    public FieldDefinition(string name, FieldDataType dataType = FieldDataType.Text, string? displayName = null)
    {
        Name = DomainGuard.Required(name, nameof(name));
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Name : displayName.Trim();
        DataType = dataType;
    }

    public string Name { get; }
    public string DisplayName { get; }
    public FieldDataType DataType { get; }
}
