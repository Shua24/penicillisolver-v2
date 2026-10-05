using System.Reflection;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Locates the repository root at test time so the tests can read the real
/// sample workbooks. The path is discovered by walking up from the test
/// assembly's own location until a directory containing <c>excel-samples</c> is
/// found, which keeps the tests free of any absolute path and working wherever
/// the repository happens to be checked out.
/// </summary>
internal static class SampleDataLocator
{
    /// <summary>The directory that holds the real sample spreadsheets.</summary>
    private const string SampleDirectoryName = "excel-samples";

    /// <summary>The csv sample file name, relative to the sample directory.</summary>
    private const string CsvSampleFileName = "amr.csv";

    /// <summary>The xlsx sample file name, relative to the sample directory.</summary>
    private const string XlsxSampleFileName = "amr.xlsx";

    /// <summary>
    /// Returns the absolute path of the real csv sample.
    /// </summary>
    internal static string CsvSamplePath()
    {
        string samplePath = BuildSamplePath(CsvSampleFileName);

        return samplePath;
    }

    /// <summary>
    /// Returns the absolute path of the real xlsx sample.
    /// </summary>
    internal static string XlsxSamplePath()
    {
        string samplePath = BuildSamplePath(XlsxSampleFileName);

        return samplePath;
    }

    /// <summary>
    /// Builds the absolute path of a sample file by walking up to the repository
    /// root and appending the sample directory and file name.
    /// </summary>
    private static string BuildSamplePath(string sampleFileName)
    {
        string repositoryRoot = FindRepositoryRoot();

        string sampleDirectory = Path.Combine(repositoryRoot, SampleDirectoryName);

        string samplePath = Path.Combine(sampleDirectory, sampleFileName);

        return samplePath;
    }

    /// <summary>
    /// Walks up from the test assembly directory until the directory holding the
    /// sample folder is found.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        Assembly testAssembly = typeof(SampleDataLocator).Assembly;

        string assemblyPath = testAssembly.Location;

        DirectoryInfo? currentDirectory = new DirectoryInfo(assemblyPath).Parent;

        while (currentDirectory is not null)
        {
            string candidatePath = Path.Combine(
                currentDirectory.FullName,
                SampleDirectoryName);

            bool candidateExists = Directory.Exists(candidatePath);

            if (candidateExists)
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate a '{SampleDirectoryName}' directory above '{assemblyPath}'.");
    }
}
