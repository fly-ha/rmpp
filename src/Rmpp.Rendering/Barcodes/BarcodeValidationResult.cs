namespace Rmpp.Rendering.Barcodes;

public sealed record BarcodeValidationIssue(string Code, string Message);

public sealed record BarcodeValidationResult(IReadOnlyList<BarcodeValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}
