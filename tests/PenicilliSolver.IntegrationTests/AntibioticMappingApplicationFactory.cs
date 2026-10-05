using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// The shared application factory extended with the test-only mapping endpoint.
/// It reuses the parent factory's private SQLite database and seeding, and adds
/// only the <see cref="AntibioticMappingTestEndpointStartupFilter"/> so the
/// integration test can post a mapping create over real HTTP.
/// </summary>
public sealed class AntibioticMappingApplicationFactory : PenicilliSolverApplicationFactory
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.AddTransient<IStartupFilter, AntibioticMappingTestEndpointStartupFilter>();
        });
    }
}
