using System.Reflection;
using Xunit;

namespace Rmpp.Domain.Tests;

/// <summary>
/// Verifies the domain test host and referenced assembly can be loaded.
/// </summary>
public sealed class ProjectSmokeTests
{
    /// <summary>
    /// Loads the production assembly by its configured name.
    /// </summary>
    [Fact]
    public void ProductionAssemblyCanBeLoaded()
    {
        Assert.NotNull(Assembly.Load("Rmpp.Domain"));
    }
}
