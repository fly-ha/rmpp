using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Rmpp.Domain.Geometry;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;
using Xunit;

namespace Rmpp.Rendering.Tests.Skia;

public sealed partial class SkiaPdfDimensionTests
{
    [Fact]
    public void A4MediaBoxRemainsWithinPointQuantizationTolerance()
    {
        MmSize a4 = new(210, 297);
        RenderPage page = new()
        {
            PageNumber = 1,
            Size = a4,
            Clip = RenderClip.FromRectangle(new MmRect(0, 0, a4.Width, a4.Height)),
            Commands = Array.Empty<RenderCommand>(),
        };
        using MemoryStream stream = new();
        new SkiaPdfExporter().Export(new RenderScene { DocumentId = Guid.NewGuid(), Pages = [page] }, stream);
        string ascii = Encoding.ASCII.GetString(stream.ToArray());
        Match match = MediaBoxRegex().Match(ascii);

        Assert.True(match.Success);
        double widthMm = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 25.4 / 72;
        double heightMm = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 25.4 / 72;
        Assert.InRange(Math.Abs(widthMm - a4.Width), 0, 0.15);
        Assert.InRange(Math.Abs(heightMm - a4.Height), 0, 0.15);
    }

    [GeneratedRegex(@"/MediaBox\s*\[\s*0\s+0\s+([0-9.]+)\s+([0-9.]+)", RegexOptions.CultureInvariant)]
    private static partial Regex MediaBoxRegex();
}
