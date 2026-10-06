using Microsoft.AspNetCore.Hosting;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// The shared application factory with every SeedAccounts value cleared, so
/// the host boots as if it had a fresh database and no development seed
/// configuration. Only the seeder's bootstrap branch can create accounts on
/// this factory.
/// </summary>
public sealed class NoSeedAccountsApplicationFactory : PenicilliSolverApplicationFactory
{
    private static readonly string[] SeedAccountKeys =
    [
        "SeedAccounts:ClinicalPathologist:Email",
        "SeedAccounts:ClinicalPathologist:Password",
        "SeedAccounts:OtherDoctor:Email",
        "SeedAccounts:OtherDoctor:Password",
        "SeedAccounts:InfectiousDiseaseControlTeam:Email",
        "SeedAccounts:InfectiousDiseaseControlTeam:Password",
    ];

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        foreach (string seedAccountKey in SeedAccountKeys)
        {
            builder.UseSetting(seedAccountKey, string.Empty);
        }
    }
}
