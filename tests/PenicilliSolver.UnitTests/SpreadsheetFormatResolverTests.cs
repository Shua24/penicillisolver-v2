using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Covers the extension based format lookup, including the cases the upload
/// path relies on: casing, an unsupported extension, and a name with no
/// extension at all.
/// </summary>
public class SpreadsheetFormatResolverTests
{
    [Theory]
    [InlineData("amr.csv", SpreadsheetFileFormat.Csv)]
    [InlineData("amr.xlsx", SpreadsheetFileFormat.Xlsx)]
    public void Resolve_KnownExtensions_ReturnsTheMatchingFormat(
        string originalFileName,
        SpreadsheetFileFormat expectedFormat)
    {
        SpreadsheetFileFormat? resolvedFormat = SpreadsheetFormatResolver.Resolve(originalFileName);

        Assert.Equal(expectedFormat, resolvedFormat);
    }

    [Theory]
    [InlineData("AMR.CSV", SpreadsheetFileFormat.Csv)]
    [InlineData("Amr.XlsX", SpreadsheetFileFormat.Xlsx)]
    public void Resolve_UppercaseExtensions_AreMatchedCaseInsensitively(
        string originalFileName,
        SpreadsheetFileFormat expectedFormat)
    {
        SpreadsheetFileFormat? resolvedFormat = SpreadsheetFormatResolver.Resolve(originalFileName);

        Assert.Equal(expectedFormat, resolvedFormat);
    }

    [Fact]
    public void Resolve_AnUnsupportedExtension_ReturnsNull()
    {
        SpreadsheetFileFormat? resolvedFormat = SpreadsheetFormatResolver.Resolve("notes.txt");

        Assert.Null(resolvedFormat);
    }

    [Fact]
    public void Resolve_AFileNameWithNoExtension_ReturnsNull()
    {
        SpreadsheetFileFormat? resolvedFormat = SpreadsheetFormatResolver.Resolve("spreadsheet");

        Assert.Null(resolvedFormat);
    }
}
