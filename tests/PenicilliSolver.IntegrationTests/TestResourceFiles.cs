using System.Xml.Linq;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// Reads the application's resource files straight off disk, so an integration
/// test can assert that a rendered page carried a string from the resx rather
/// than a literal the test itself supplied.
/// </summary>
/// <remarks>
/// Asserting a translated sentence directly makes the test a second copy of the
/// translation: it fails the moment somebody improves the wording, and it passes
/// even if the page never consulted the resx. Comparing against the file's own
/// value tests the wiring instead of the copy.
/// </remarks>
internal static class TestResourceFiles
{
    /// <summary>The neutral (English) resource file name.</summary>
    public const string NeutralFileName = "SharedResource.resx";

    /// <summary>The Indonesian resource file name.</summary>
    public const string IndonesianFileName = "SharedResource.id.resx";

    /// <summary>
    /// Loads one resource file as a key to value map, using ordinal comparison
    /// so a key is matched exactly as the localizer matches it.
    /// </summary>
    /// <param name="fileName">The resource file name, not a path.</param>
    /// <returns>Every key in the file with its value.</returns>
    public static IReadOnlyDictionary<string, string> LoadValues(string fileName)
    {
        XDocument document = XDocument.Load(ResolvePath(fileName));

        Dictionary<string, string> values = new(StringComparer.Ordinal);

        foreach (XElement element in document.Root!.Elements("data"))
        {
            string? name = element.Attribute("name")?.Value;
            string? value = element.Element("value")?.Value;

            if (!string.IsNullOrEmpty(name) && value is not null)
            {
                values[name] = value;
            }
        }

        return values;
    }

    /// <summary>
    /// Locates a resource file by walking up from the test assembly to the
    /// repository root, so the test does not depend on the copy-to-output
    /// setting of the web project.
    /// </summary>
    /// <param name="fileName">The resource file name, not a path.</param>
    /// <returns>The absolute path of the resource file.</returns>
    public static string ResolvePath(string fileName)
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
