using Rmpp.Domain.Documents;
using Xunit;

namespace Rmpp.Domain.Tests.Documents;

public sealed class TemplateDocumentTests
{
    [Fact]
    public void NewDocumentUsesA4AndDefaultLayer()
    {
        TemplateDocument document = TemplateDocument.CreateNew("Sample");

        Assert.Equal("Sample", document.Metadata.Title);
        Assert.Equal(210, document.Page.Media.Size.Width);
        Assert.Equal(297, document.Page.Media.Size.Height);
        Assert.Single(document.Layers);
        Assert.Equal(1, document.PrintSettings.OutputCount);
        Assert.Equal(1, document.PrintSettings.Copies);
        Assert.Null(document.PrintSettings.PreferredPrinterId);
    }
}
