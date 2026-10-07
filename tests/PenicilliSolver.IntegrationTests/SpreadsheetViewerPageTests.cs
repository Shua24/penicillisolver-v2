using System.Net;

using Microsoft.Extensions.DependencyInjection;

using penicillisolver_v2.Domain.ValueObjects;
using penicillisolver_v2.Services;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// The susceptibility ranking page end to end: it renders, it offers the organisms
/// that are really in the current file, and it ranks a real one.
/// </summary>
/// <remarks>
/// Split from <see cref="Phase6PageAccessTests"/>, which covers who may reach
/// each page. This file covers what the viewer page does once reached. Both
/// share <see cref="IntegrationTestBase"/> and its seeding, and neither writes
/// to the application's real storage directory — see the remarks on
/// <see cref="PenicilliSolverApplicationFactory"/> for why that matters.
/// </remarks>
public sealed class SpreadsheetViewerPageTests : IntegrationTestBase
{
    /// <summary>Creates the test class over the shared application factory.</summary>
    public SpreadsheetViewerPageTests(PenicilliSolverApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Viewer_page_resolves_and_ranks_a_real_organism()
    {
        // End to end on the seed file: resolving a real name yields a ranking,
        // and the leader is the most susceptible measured drug. For Organism
        // one the seed holds Cefetamet=0, Cefixime=1, Ceftibuten=2,
        // Amoxicillin=90, so the two highest readings lead, in value order.
        await SeedStoredSpreadsheetAsync();

        using IServiceScope scope = Factory.Services.CreateScope();

        SpreadsheetQueryService queryService =
            scope.ServiceProvider.GetRequiredService<SpreadsheetQueryService>();

        OrganismLookupResult lookupResult =
            await queryService.ResolveOrganismAsync("Organism one");

        Assert.Equal(OrganismLookupOutcome.Resolved, lookupResult.Outcome);

        IReadOnlyList<AntibioticSusceptibility> ranking =
            await queryService.GetTopSusceptibleWithinOrganismAsync("Organism one", 2);

        Assert.Equal(2, ranking.Count);
        Assert.Equal("Amoxicillin", ranking[0].AntibioticName);
        Assert.Equal(90.0, ranking[0].Value.Percent!.Value, tolerance: 1e-9);
        Assert.Equal("Ceftibuten", ranking[1].AntibioticName);
        Assert.Equal(2.0, ranking[1].Value.Percent!.Value, tolerance: 1e-9);

        bool everyRowWasMeasured = ranking.All(row => row.Value.IsMeasured);

        Assert.True(everyRowWasMeasured);
    }

    [Fact]
    public async Task Viewer_page_offers_the_real_organism_names_from_the_uploaded_file()
    {
        // The regression this covers: the picker must offer the names that are
        // actually in the current file, not a placeholder. The names are checked
        // through the same service the page uses, because the dropdown is closed
        // on first paint by design — it opens when the reader focuses or types.
        await SeedStoredSpreadsheetAsync();

        using IServiceScope scope = Factory.Services.CreateScope();

        SpreadsheetQueryService queryService =
            scope.ServiceProvider.GetRequiredService<SpreadsheetQueryService>();

        IReadOnlyList<string> organismNames = await queryService.GetOrganismNamesAsync();

        Assert.Contains("Organism one", organismNames);
        Assert.Contains("Organism two", organismNames);

        // The seed file's names are the only ones served. A likely-sounding
        // organism that is not in the file must never be offered, or the reader
        // would pick a name the ranking cannot resolve.
        Assert.DoesNotContain("Acinetobacter baumannii", organismNames);
        Assert.Equal(2, organismNames.Count);
    }

    [Fact]
    public async Task Viewer_page_does_not_overwrite_the_developers_stored_spreadsheet()
    {
        // The bug that made the picker look broken: the test host's content root
        // is the project directory, so spreadsheet storage resolved to the real
        // App_Data/spreadsheets and every integration run wrote its seed file
        // over whatever the developer had uploaded. The stored file is the
        // application's data, and the tests must not touch it.
        string realStorageDirectory = Path.Combine(
            LocateSourceDirectory(),
            "App_Data",
            "spreadsheets");

        string realStoredFilePath = Path.Combine(realStorageDirectory, "current-spreadsheet.csv");

        string? contentBefore = File.Exists(realStoredFilePath)
            ? await File.ReadAllTextAsync(realStoredFilePath)
            : null;

        await SeedStoredSpreadsheetAsync();

        string? contentAfter = File.Exists(realStoredFilePath)
            ? await File.ReadAllTextAsync(realStoredFilePath)
            : null;

        Assert.Equal(contentBefore, contentAfter);

        // And the service really is pointed somewhere else.
        using IServiceScope scope = Factory.Services.CreateScope();

        SpreadsheetStorageService storageService =
            scope.ServiceProvider.GetRequiredService<SpreadsheetStorageService>();

        bool storageIsRedirected = !string.Equals(
            Path.GetFullPath(storageService.StorageDirectoryPath),
            Path.GetFullPath(realStorageDirectory),
            StringComparison.Ordinal);

        Assert.True(
            storageIsRedirected,
            $"Storage still points at the developer's data: {storageService.StorageDirectoryPath}");
    }

    [Fact]
    public async Task Viewer_page_renders_the_picker_and_no_native_datalist()
    {
        await SeedStoredSpreadsheetAsync();

        HttpClient client = await SignInOtherDoctorAsync();

        HttpResponseMessage response = await client.GetAsync("/spreadsheet-viewer");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"organism-name\"", html, StringComparison.Ordinal);

        // The picker is a rendered list the page controls, because a datalist
        // matches by prefix only and this file's qualified names are not
        // findable that way.
        Assert.DoesNotContain("<datalist", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Viewer_page_withholds_the_ranking_and_count_until_an_organism_is_resolved()
    {
        // The count only means something relative to a chosen organism, and the
        // ranking table needs one, so neither is shown against a ranking that
        // does not exist yet.
        await SeedStoredSpreadsheetAsync();

        HttpClient client = await SignInOtherDoctorAsync();

        HttpResponseMessage response = await client.GetAsync("/spreadsheet-viewer");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("id=\"top-count\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("% susceptible", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Viewer_page_does_not_render_a_global_ranking()
    {
        // The global average ranking was removed (Q6). Its heading must not
        // survive anywhere on the page.
        await SeedStoredSpreadsheetAsync();

        HttpClient client = await SignInOtherDoctorAsync();

        HttpResponseMessage response = await client.GetAsync("/spreadsheet-viewer");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Antibiotics with no data", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Walks up from the test assembly to the repository's source directory, so
    /// the test can check the developer's real storage directory is untouched.
    /// </summary>
    private static string LocateSourceDirectory()
    {
        DirectoryInfo? currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            string candidatePath = Path.Combine(currentDirectory.FullName, "src");

            if (Directory.Exists(candidatePath))
            {
                return candidatePath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository's src directory.");
    }
}
