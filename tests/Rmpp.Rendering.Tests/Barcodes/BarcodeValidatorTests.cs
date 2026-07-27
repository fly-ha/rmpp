using Rmpp.Domain.Elements;
using Rmpp.Rendering.Barcodes;
using Xunit;

namespace Rmpp.Rendering.Tests.Barcodes;

public sealed class BarcodeValidatorTests
{
    [Theory]
    [InlineData(BarcodeSymbology.Code128, "RMPP-128")]
    [InlineData(BarcodeSymbology.Code39, "RMPP-39")]
    [InlineData(BarcodeSymbology.Ean13, "5901234123457")]
    [InlineData(BarcodeSymbology.Ean8, "96385074")]
    [InlineData(BarcodeSymbology.UpcA, "036000291452")]
    [InlineData(BarcodeSymbology.Interleaved2Of5, "123456")]
    [InlineData(BarcodeSymbology.Codabar, "A1234B")]
    [InlineData(BarcodeSymbology.QrCode, "红枫 RMPP")]
    [InlineData(BarcodeSymbology.DataMatrix, "RMPP-DM")]
    public void VersionOneSymbologiesAcceptRepresentativeContent(BarcodeSymbology symbology, string content)
    {
        BarcodeValidationResult result = new BarcodeValidator().Validate(new BarcodeOptions
        {
            Symbology = symbology,
            Content = content,
        });

        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(static issue => issue.Message)));
    }

    [Theory]
    [InlineData(BarcodeSymbology.Ean13, "5901234123458", "invalid-check-digit")]
    [InlineData(BarcodeSymbology.Interleaved2Of5, "12345", "invalid-itf")]
    [InlineData(BarcodeSymbology.Code39, "lowercase", "invalid-code39-character")]
    [InlineData(BarcodeSymbology.Codabar, "12345", "invalid-codabar-character")]
    public void InvalidContentProducesStructuredIssue(BarcodeSymbology symbology, string content, string code)
    {
        BarcodeValidationResult result = new BarcodeValidator().Validate(new BarcodeOptions
        {
            Symbology = symbology,
            Content = content,
        });

        Assert.Contains(result.Issues, issue => issue.Code == code);
    }
}
