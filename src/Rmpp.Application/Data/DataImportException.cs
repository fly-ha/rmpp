namespace Rmpp.Application.Data;

/// <summary>表示带安全行列位置的数据导入错误，不回显整条敏感记录。</summary>
public sealed class DataImportException : Exception
{
    public DataImportException(string message, int? rowNumber = null, int? columnNumber = null, Exception? innerException = null)
        : base(message, innerException)
    {
        RowNumber = rowNumber;
        ColumnNumber = columnNumber;
    }

    public int? RowNumber { get; }

    public int? ColumnNumber { get; }
}
