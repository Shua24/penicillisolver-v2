using penicillisolver_v2.Domain.ValueObjects;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Covers the rule that decides whether an antibiotic name needs a mapped
/// meaning (requirements 8, and the clarification that a file which already
/// spells its antibiotics out in full needs no mapping at all).
/// </summary>
/// <remarks>
/// The two real samples are the reference: amr.xlsx names its columns as short
/// codes with a susceptibility marker and must be mapped; amr.csv names them in
/// full and must not be. Both are asserted against the real files elsewhere, so
/// these tests pin the rule itself.
/// </remarks>
public class AntibioticNameClassifierTests
{
    [Theory]
    [InlineData("AMK %S")]
    [InlineData("AMX %S")]
    [InlineData("GAT %S")]
    [InlineData("amk %s")]
    [InlineData("  AMK %S  ")]
    public void IsAbbreviation_TrueForNamesCarryingTheSusceptibilityMarker(
        string antibioticName)
    {
        bool isAbbreviation = AntibioticNameClassifier.IsAbbreviation(antibioticName);

        Assert.True(isAbbreviation);
    }

    [Theory]
    [InlineData("Amoxicillin")]
    [InlineData("Amoxicillin/Clavulanic acid")]
    [InlineData("Amphotericin B")]
    [InlineData("Ampicillin/Sulbactam")]
    public void IsAbbreviation_FalseForCompleteAntibioticNames(string antibioticName)
    {
        bool isAbbreviation = AntibioticNameClassifier.IsAbbreviation(antibioticName);

        Assert.False(isAbbreviation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsAbbreviation_FalseForEmptyInput(string? antibioticName)
    {
        bool isAbbreviation = AntibioticNameClassifier.IsAbbreviation(antibioticName);

        Assert.False(isAbbreviation);
    }

    [Fact]
    public void IsAbbreviation_IsNotFooledByAMarkerInACompleteName()
    {
        // The marker decides, wherever it appears. This documents the behaviour
        // rather than endorsing such a column name, which no sample contains.
        bool isAbbreviation = AntibioticNameClassifier.IsAbbreviation("Amoxicillin %S");

        Assert.True(isAbbreviation);
    }

    [Fact]
    public void NeedsNoMapping_TrueWhenEveryNameIsComplete()
    {
        string[] antibioticNames =
        [
            "Amoxicillin",
            "Amoxicillin/Clavulanic acid",
            "Amphotericin B",
        ];

        bool needsNoMapping = AntibioticNameClassifier.NeedsNoMapping(antibioticNames);

        Assert.True(needsNoMapping);
    }

    [Fact]
    public void NeedsNoMapping_FalseWhenAnyNameIsAnAbbreviation()
    {
        // A single abbreviated name is enough to make the mapping workflow
        // applicable: the file is mixed, and the abbreviation still needs a
        // meaning.
        string[] antibioticNames =
        [
            "Amoxicillin",
            "AMK %S",
            "Colistin",
        ];

        bool needsNoMapping = AntibioticNameClassifier.NeedsNoMapping(antibioticNames);

        Assert.False(needsNoMapping);
    }

    [Fact]
    public void NeedsNoMapping_TrueForAFileWithNoAntibiotics()
    {
        // Nothing to map means no mapping workflow, not a broken page.
        string[] antibioticNames = [];

        bool needsNoMapping = AntibioticNameClassifier.NeedsNoMapping(antibioticNames);

        Assert.True(needsNoMapping);
    }

    [Fact]
    public void NeedsNoMapping_ThrowsForANullList()
    {
        Assert.Throws<ArgumentNullException>(
            () => AntibioticNameClassifier.NeedsNoMapping(null!));
    }
}
