using Rmpp.Rendering.Text;
using Xunit;

namespace Rmpp.Rendering.Tests.Text;

public sealed class LocalFontCatalogTests
{
    [Fact]
    public void InstalledFamilyResolvesWithoutFallback()
    {
        LocalFontCatalog catalog = new();
        string family = Assert.Single(catalog.Families.Take(1));

        FontResolution resolution = catalog.Resolve(family);

        Assert.False(resolution.UsedFallback);
        Assert.Equal(family, resolution.ResolvedFamily, ignoreCase: true);
    }

    [Fact]
    public void MissingFamilyUsesDeterministicLocalFallback()
    {
        LocalFontCatalog catalog = new();

        FontResolution first = catalog.Resolve("__RMPP_MISSING_FONT__");
        FontResolution second = catalog.Resolve("__RMPP_MISSING_FONT__");

        Assert.True(first.UsedFallback);
        Assert.Equal(first, second);
        Assert.Contains(first.ResolvedFamily, catalog.Families, StringComparer.OrdinalIgnoreCase);
    }
}
