using System.IO;
using Rmpp.Desktop.ViewModels;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Xunit;

namespace Rmpp.Desktop.Tests.ViewModels;

public sealed class TemplateLifecycleTests
{
    [Fact]
    public async Task SaveOpenAndDeleteTemplateUseTheLocalPackageAsDocumentTruth()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"rmpp-template-lifecycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "验收模板.rmpp");
        try
        {
            MainWindowViewModel writer = new();
            writer.ActiveDocument!.Designer.CreateElement(
                DesignerTool.Ellipse,
                new MmRect(10, 20, 30, 40));

            string savedPath = await writer.SaveActiveTemplateAsync(path);

            Assert.Equal(Path.GetFullPath(path), savedPath);
            Assert.True(File.Exists(path));
            Assert.False(writer.ActiveDocument.IsDirty);
            Assert.Equal(savedPath, writer.ActiveDocument.OriginalTemplatePath);

            MainWindowViewModel reader = new();
            await reader.OpenTemplateAsync(path);

            Assert.Equal(savedPath, reader.ActiveDocument!.OriginalTemplatePath);
            Assert.IsType<EllipseElement>(Assert.Single(reader.ActiveDocument.Session.State.Document.Elements));

            await reader.DeleteTemplateAsync(path);

            Assert.False(File.Exists(path));
            Assert.DoesNotContain(reader.Documents, tab =>
                string.Equals(tab.OriginalTemplatePath, savedPath, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(reader.ActiveDocument);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
