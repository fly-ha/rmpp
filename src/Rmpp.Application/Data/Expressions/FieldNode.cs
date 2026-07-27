namespace Rmpp.Application.Data.Expressions;

/// <summary>通过显式方括号名称引用数据字段，不允许属性链或反射访问。</summary>
public sealed record FieldNode(
    string FieldName,
    ExpressionTextSpan Span) : ExpressionNode(Span);
