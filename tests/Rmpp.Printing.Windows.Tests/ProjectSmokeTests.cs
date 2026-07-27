using System.Reflection;
using Xunit;

namespace Rmpp.Printing.Windows.Tests;

/// <summary>
/// Verifies the Windows printing test host and referenced assembly can be loaded.
/// </summary>
public sealed class ProjectSmokeTests
{
    /// <summary>
    /// Loads the production assembly by its configured name.
    /// </summary>
    [Fact]
    public void ProductionAssemblyCanBeLoaded() => Assert.NotNull(Assembly.Load("Rmpp.Printing.Windows"));
}
