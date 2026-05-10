using SharpClaw.Testing.Harness;

namespace SharpClaw.Testing.Xunit;

/// <summary>
/// Loads scenario files into xUnit <c>MemberData</c> rows.
/// </summary>
public static class XunitScenarioData
{
    /// <summary>
    /// Loads all JSON scenarios from a directory.
    /// </summary>
    /// <param name="directory">Scenario directory.</param>
    /// <returns>xUnit theory data rows containing one scenario each.</returns>
    public static IEnumerable<object[]> LoadDirectory(string directory)
    {
        var loader = new JsonScenarioLoader();
        var scenarios = loader.LoadDirectoryAsync(directory, CancellationToken.None).GetAwaiter().GetResult();
        foreach (var scenario in scenarios)
        {
            yield return [scenario];
        }
    }
}
