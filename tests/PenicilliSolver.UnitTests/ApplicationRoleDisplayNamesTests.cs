using penicillisolver_v2.Domain.Constants;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Covers the mapping from the stable role tokens to the labels the UI shows.
/// </summary>
public class ApplicationRoleDisplayNamesTests
{
    [Fact]
    public void DisplayNameOf_maps_the_clinical_pathologist_token()
    {
        string displayName = ApplicationRoleDisplayNames.DisplayNameOf(
            ApplicationRoleNames.ClinicalPathologist);

        Assert.Equal("Clinical Pathologist", displayName);
    }

    [Fact]
    public void DisplayNameOf_maps_the_other_doctor_token()
    {
        string displayName = ApplicationRoleDisplayNames.DisplayNameOf(
            ApplicationRoleNames.OtherDoctor);

        Assert.Equal("Doctor", displayName);
    }

    [Fact]
    public void DisplayNameOf_maps_the_infectious_disease_control_team_token()
    {
        string displayName = ApplicationRoleDisplayNames.DisplayNameOf(
            ApplicationRoleNames.InfectiousDiseaseControlTeam);

        Assert.Equal("Infectious Disease Control Team", displayName);
    }

    [Fact]
    public void DisplayNameOf_returns_an_unknown_token_unchanged()
    {
        string displayName = ApplicationRoleDisplayNames.DisplayNameOf("Radiologist");

        Assert.Equal("Radiologist", displayName);
    }

    [Fact]
    public void DisplayNameOf_throws_when_the_token_is_null()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ApplicationRoleDisplayNames.DisplayNameOf(null!));

        Assert.Equal("roleToken", exception.ParamName);
    }
}
