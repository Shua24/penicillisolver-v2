using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.ValueObjects;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Builds the test documents the ranking tests share.
/// </summary>
/// <remarks>
/// Kept in one place so the two ranking test files describe the same shape of
/// document. It lives in its own type rather than in a test class because both
/// files need it and a test class is not a place to hang shared fixtures.
/// </remarks>
internal static class RankingTestDocument
{
    /// <summary>
    /// Creates a document from a set of antibiotic names and measurements.
    /// </summary>
    /// <param name="antibioticNames">Every antibiotic the document should list.</param>
    /// <param name="measurements">The measurements present.</param>
    /// <param name="organismNames">The declared organism list, or null to infer it.</param>
    /// <returns>The document to rank against.</returns>
    internal static SpreadsheetDocument Build(
        IReadOnlyList<string> antibioticNames,
        IReadOnlyList<SusceptibilityMeasurement> measurements,
        IReadOnlyList<string>? organismNames = null)
    {
        // The document's organism list defaults to the organisms the
        // measurements actually name. Declaring one list while measuring another
        // made the ranking's presence check reject the very organism under test.
        IReadOnlyList<string> declaredOrganisms = organismNames
            ?? measurements
                .Select(measurement => measurement.OrganismName)
                .Distinct(StringComparer.Ordinal)
                .ToList();

        SpreadsheetDocument document = new SpreadsheetDocument(
            originalFileName: "test.csv",
            fileFormat: SpreadsheetFileFormat.Csv,
            orientation: SpreadsheetOrientation.AntibioticsAsRows,
            organismNames: declaredOrganisms,
            antibioticNames: antibioticNames,
            measurements: measurements);

        return document;
    }
}
