using System.Text;
using Rmpp.Domain.Elements;

namespace Rmpp.Rendering.Barcodes;

/// <summary>在进入 ZXing 前执行确定性的字符集、长度和校验位检查。</summary>
public sealed class BarcodeValidator
{
    private const string Code39Characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%";
    private const string CodabarMiddleCharacters = "0123456789-$:/.+";
    private readonly HashSet<BarcodeSymbology> supported =
        SupportedBarcodeSymbologies.Version1.ToHashSet();

    public BarcodeValidationResult Validate(BarcodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<BarcodeValidationIssue> issues = [];
        if (!supported.Contains(options.Symbology))
        {
            issues.Add(new BarcodeValidationIssue("unsupported-symbology", "Barcode symbology is not supported in version 1.0."));
            return new BarcodeValidationResult(issues);
        }

        if (string.IsNullOrEmpty(options.Content))
        {
            issues.Add(new BarcodeValidationIssue("empty-barcode", "Barcode content cannot be empty."));
            return new BarcodeValidationResult(issues);
        }

        if (!double.IsFinite(options.QuietZoneMm) || options.QuietZoneMm < 0)
        {
            issues.Add(new BarcodeValidationIssue("invalid-quiet-zone", "Quiet zone must be a non-negative finite millimetre value."));
        }

        switch (options.Symbology)
        {
            case BarcodeSymbology.Code128:
                ValidateLength(options.Content, 200, issues);
                break;
            case BarcodeSymbology.Code39:
                ValidateCode39(options.Content, issues);
                break;
            case BarcodeSymbology.Ean13:
                ValidateRetail(options.Content, 12, 13, issues);
                break;
            case BarcodeSymbology.Ean8:
                ValidateRetail(options.Content, 7, 8, issues);
                break;
            case BarcodeSymbology.UpcA:
                ValidateRetail(options.Content, 11, 12, issues);
                break;
            case BarcodeSymbology.Interleaved2Of5:
                ValidateItf(options.Content, issues);
                break;
            case BarcodeSymbology.Codabar:
                ValidateCodabar(options.Content, issues);
                break;
            case BarcodeSymbology.QrCode:
                ValidateByteLength(options.Content, 2953, issues);
                ValidateErrorCorrection(options.ErrorCorrectionLevel, issues);
                break;
            case BarcodeSymbology.DataMatrix:
                ValidateByteLength(options.Content, 1558, issues);
                break;
        }

        return new BarcodeValidationResult(issues);
    }

    private static void ValidateLength(string content, int maximum, List<BarcodeValidationIssue> issues)
    {
        if (content.Length > maximum)
        {
            issues.Add(new BarcodeValidationIssue("barcode-too-long", $"Barcode content exceeds {maximum} characters."));
        }
    }

    private static void ValidateCode39(string content, List<BarcodeValidationIssue> issues)
    {
        ValidateLength(content, 80, issues);
        if (content.Any(character => !Code39Characters.Contains(character, StringComparison.Ordinal)))
        {
            issues.Add(new BarcodeValidationIssue("invalid-code39-character", "Code 39 supports only 0-9, A-Z, space and - . $ / + %."));
        }
    }

    private static void ValidateRetail(string content, int dataLength, int fullLength, List<BarcodeValidationIssue> issues)
    {
        if (!content.All(char.IsAsciiDigit) || content.Length != dataLength && content.Length != fullLength)
        {
            issues.Add(new BarcodeValidationIssue("invalid-retail-barcode", $"Barcode requires {dataLength} data digits or {fullLength} digits including the check digit."));
            return;
        }

        if (content.Length == fullLength && ComputeModulo10(content.AsSpan(0, dataLength)) != content[^1] - '0')
        {
            issues.Add(new BarcodeValidationIssue("invalid-check-digit", "Retail barcode check digit is invalid."));
        }
    }

    private static void ValidateItf(string content, List<BarcodeValidationIssue> issues)
    {
        if (!content.All(char.IsAsciiDigit) || content.Length < 2 || content.Length > 80 || content.Length % 2 != 0)
        {
            issues.Add(new BarcodeValidationIssue("invalid-itf", "ITF requires an even number of 2 to 80 digits."));
        }
    }

    private static void ValidateCodabar(string content, List<BarcodeValidationIssue> issues)
    {
        if (content.Length < 3 || content.Length > 80)
        {
            issues.Add(new BarcodeValidationIssue("invalid-codabar-length", "Codabar requires 3 to 80 characters."));
            return;
        }

        char start = char.ToUpperInvariant(content[0]);
        char end = char.ToUpperInvariant(content[^1]);
        if (start is < 'A' or > 'D' || end is < 'A' or > 'D'
            || content[1..^1].Any(character => !CodabarMiddleCharacters.Contains(char.ToUpperInvariant(character), StringComparison.Ordinal)))
        {
            issues.Add(new BarcodeValidationIssue("invalid-codabar-character", "Codabar must start and end with A-D and contain only 0-9, - $ : / . + inside."));
        }
    }

    private static void ValidateByteLength(string content, int maximum, List<BarcodeValidationIssue> issues)
    {
        if (Encoding.UTF8.GetByteCount(content) > maximum)
        {
            issues.Add(new BarcodeValidationIssue("barcode-too-long", $"UTF-8 content exceeds {maximum} bytes."));
        }
    }

    private static void ValidateErrorCorrection(int level, List<BarcodeValidationIssue> issues)
    {
        if (level is < 0 or > 3)
        {
            issues.Add(new BarcodeValidationIssue("invalid-error-correction", "QR error correction level must be 0 to 3."));
        }
    }

    private static int ComputeModulo10(ReadOnlySpan<char> digits)
    {
        int sum = 0;
        bool multiplyByThree = true;
        for (int index = digits.Length - 1; index >= 0; index--)
        {
            int digit = digits[index] - '0';
            sum += multiplyByThree ? digit * 3 : digit;
            multiplyByThree = !multiplyByThree;
        }

        return (10 - sum % 10) % 10;
    }
}
