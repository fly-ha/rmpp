using Rmpp.Domain.Geometry;
using Rmpp.Domain.Elements;
using Rmpp.Rendering.Barcodes;
using Rmpp.Rendering.Geometry;
using Xunit;

namespace Rmpp.Rendering.Tests.Fuzz;

public sealed class GeometryBarcodeFuzzTests
{
    [Fact]
    public void RandomConvexPolygonsNormalizeToPositiveFiniteArea()
    {
        Random random = new(314159);
        for (int iteration = 0; iteration < 500; iteration++)
        {
            int count = random.Next(3, 30);
            MmPoint[] points = Enumerable.Range(0, count).Select(index =>
            {
                double angle = Math.PI * 2 * index / count;
                double radius = 1 + random.NextDouble() * 100;
                return new MmPoint(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
            }).ToArray();
            MmPoint[] normalized = PolygonGeometryBuilder.NormalizeWinding(points);
            double area = PolygonGeometryBuilder.SignedArea(normalized);
            Assert.True(double.IsFinite(area));
            Assert.True(area > 0);
        }
    }

    [Fact]
    public void RandomBarcodeInputsReturnValidationInsteadOfThrowing()
    {
        Random random = new(271828);
        BarcodeValidator validator = new();
        BarcodeSymbology[] types = Enum.GetValues<BarcodeSymbology>();
        for (int iteration = 0; iteration < 1_000; iteration++)
        {
            string content = new(Enumerable.Range(0, random.Next(0, 240)).Select(_ => (char)random.Next(32, 0x300)).ToArray());
            BarcodeValidationResult result = validator.Validate(new BarcodeOptions
            {
                Symbology = types[random.Next(types.Length)],
                Content = content,
                QuietZoneMm = random.NextDouble() * 10,
                ErrorCorrectionLevel = random.Next(-2, 6),
            });
            Assert.NotNull(result.Issues);
        }
    }
}
