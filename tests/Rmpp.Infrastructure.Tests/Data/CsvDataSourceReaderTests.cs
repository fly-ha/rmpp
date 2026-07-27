using System.Text;
using Rmpp.Application.Data;
using Rmpp.Infrastructure.Data;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Data;

public sealed class CsvDataSourceReaderTests
{
    [Fact]
    public async Task ReadsChineseQuotedNewlinesNumbersAndEmptyValues()
    {
        string path = TemporaryPath("csv");
        try
        {
            await File.WriteAllTextAsync(
                path,
                "编号,名称,备注,金额,空值\r\nA1,红枫叶,\"第一行\r\n第二行\",12.50,\r\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            DataSetSnapshot result = await new CsvDataSourceReader().ReadAsync(new DataImportOptions
            {
                FilePath = path,
                CultureName = "zh-CN",
            });

            DataRowSnapshot row = Assert.Single(result.Rows);
            Assert.Equal("红枫叶", row.GetValue("名称"));
            Assert.Equal("第一行\r\n第二行", row.GetValue("备注"));
            Assert.Equal(string.Empty, row.GetValue("空值"));
            Assert.Equal(Rmpp.Domain.Data.FieldDataType.Number, result.Schema.Find("金额")?.DataType);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadsSelectedGb18030Encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string path = TemporaryPath("csv");
        try
        {
            await File.WriteAllBytesAsync(path, Encoding.GetEncoding("GB18030").GetBytes("名称\n红枫叶\n"));

            DataSetSnapshot result = await new CsvDataSourceReader().ReadAsync(new DataImportOptions
            {
                FilePath = path,
                EncodingName = "GB18030",
            });

            Assert.Equal("红枫叶", Assert.Single(result.Rows).GetValue("名称"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task EnforcesRowLimitAndMarksSnapshotTruncated()
    {
        string path = TemporaryPath("csv");
        try
        {
            await File.WriteAllTextAsync(path, "Id\n1\n2\n3\n", Encoding.UTF8);

            DataSetSnapshot result = await new CsvDataSourceReader().ReadAsync(new DataImportOptions
            {
                FilePath = path,
                MaximumRows = 2,
            });

            Assert.Equal(2, result.Rows.Count);
            Assert.True(result.IsTruncated);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MalformedQuotedFieldReportsRowAndColumnWithoutRowContent()
    {
        string path = TemporaryPath("csv");
        try
        {
            await File.WriteAllTextAsync(path, "Name\n\"secret-not-closed", Encoding.UTF8);

            DataImportException error = await Assert.ThrowsAsync<DataImportException>(() =>
                new CsvDataSourceReader().ReadAsync(new DataImportOptions { FilePath = path }));

            Assert.Equal(2, error.RowNumber);
            Assert.Equal(1, error.ColumnNumber);
            Assert.DoesNotContain("secret-not-closed", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PreCancelledImportStopsBeforeProducingRows()
    {
        string path = TemporaryPath("csv");
        try
        {
            await File.WriteAllTextAsync(path, "Id\n1\n2\n", Encoding.UTF8);
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new CsvDataSourceReader().ReadAsync(
                    new DataImportOptions { FilePath = path },
                    cancellation.Token));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string TemporaryPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"rmpp-data-{Guid.NewGuid():N}.{extension}");
}
