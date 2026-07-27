using System.Diagnostics;
using Rmpp.Desktop.Controls;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Xunit;

namespace Rmpp.Desktop.Tests.Performance;

public sealed class DesignerPerformanceTests
{
    [Fact, Trait("Category", "Performance")]
    public void MeasuresTwoThousandElementsWithinThresholdOnSta()
    {
        Exception? failure = null;
        TimeSpan elapsed = TimeSpan.Zero;
        Thread thread = new(() =>
        {
            try
            {
                TemplateDocument document = TemplateDocument.CreateNew("画布性能");
                Guid layer = document.Layers[0].Id;
                document = document with
                {
                    Elements = Enumerable.Range(0, 2_000).Select(index => (TemplateElement)new RectangleElement
                    {
                        Name = "E" + index,
                        LayerId = layer,
                        Bounds = new MmRect(index % 100, index / 100, 1, 1),
                    }).ToArray(),
                };
                DesignerSurface surface = new() { Document = document, Zoom = 1 };
                Stopwatch watch = Stopwatch.StartNew();
                surface.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                watch.Stop();
                elapsed = watch.Elapsed;
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
        Assert.True(elapsed < TimeSpan.FromSeconds(5), $"画布测量耗时 {elapsed}");
    }
}
