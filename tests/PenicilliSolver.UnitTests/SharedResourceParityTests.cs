using System.Xml.Linq;

using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Guards the two resource files against drifting apart.
/// </summary>
/// <remarks>
/// This is the one failure the compiler cannot catch. A key present only in the
/// Indonesian file has no neutral fallback, and a key present only in the
/// neutral file leaves the Indonesian interface showing English. Neither breaks
/// the build, so without this test the drift would only show up as a stray
/// English sentence in an Indonesian screen.
/// </remarks>
public sealed class SharedResourceParityTests
{
    [Fact]
    public void Every_key_has_an_entry_in_both_languages()
    {
        HashSet<string> neutralKeys = ReadKeys("SharedResource.resx");
        HashSet<string> indonesianKeys = ReadKeys("SharedResource.id.resx");

        Assert.NotEmpty(neutralKeys);

        List<string> missingFromIndonesian = neutralKeys.Except(indonesianKeys).Order().ToList();
        List<string> missingFromNeutral = indonesianKeys.Except(neutralKeys).Order().ToList();

        Assert.True(
            missingFromIndonesian.Count == 0,
            "Keys with no Indonesian translation: " + string.Join(", ", missingFromIndonesian));

        Assert.True(
            missingFromNeutral.Count == 0,
            "Keys with no neutral fallback: " + string.Join(", ", missingFromNeutral));
    }

    [Fact]
    public void Every_key_has_a_non_empty_value_in_both_languages()
    {
        AssertNoEmptyValues("SharedResource.resx");
        AssertNoEmptyValues("SharedResource.id.resx");
    }

    private static HashSet<string> ReadKeys(string fileName)
    {
        XDocument document = XDocument.Load(ResolveResourcePath(fileName));

        IEnumerable<string> names = document.Root!
            .Elements("data")
            .Select(element => element.Attribute("name")?.Value)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!);

        return new HashSet<string>(names, StringComparer.Ordinal);
    }

    private static void AssertNoEmptyValues(string fileName)
    {
        XDocument document = XDocument.Load(ResolveResourcePath(fileName));

        List<string> emptyKeys = document.Root!
            .Elements("data")
            .Where(element =>
                string.IsNullOrWhiteSpace(element.Element("value")?.Value))
            .Select(element => element.Attribute("name")?.Value ?? "(unnamed)")
            .ToList();

        Assert.True(
            emptyKeys.Count == 0,
            $"Empty values in {fileName}: " + string.Join(", ", emptyKeys));
    }

    /// <summary>
    /// Locates a resource file by walking up from the test assembly to the
    /// repository root, so the test does not depend on the copy-to-output
    /// setting of the web project.
    /// </summary>
    private static string ResolveResourcePath(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "src",
                "Resources",
                fileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate 'src/Resources/{fileName}' above the test directory.");
    }
}
