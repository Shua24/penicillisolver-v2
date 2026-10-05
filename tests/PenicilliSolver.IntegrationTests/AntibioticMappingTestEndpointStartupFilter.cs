using System.Net;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.EntityFrameworkCore;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.ValueObjects;
using penicillisolver_v2.Services;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// A test-only endpoint that calls the real antibiotic abbreviation mapping
/// service with the request's authenticated principal. Registering it here — in
/// the test host — keeps production free of any test surface while still making
/// the write go through the real cookie authentication, the real authorization
/// policies, and the real server-side service gate, exactly as a page handler
/// would in the running application.
/// </summary>
public sealed class AntibioticMappingTestEndpointStartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return application =>
        {
            application.Use(async (context, nextMiddleware) =>
            {
                bool isCreateRequest = context.Request.Path == "/test/antibiotic-mappings"
                    && HttpMethods.IsPost(context.Request.Method);

                if (!isCreateRequest)
                {
                    await nextMiddleware();
                    return;
                }

                await HandleCreateAsync(context);
            });

            next(application);
        };
    }

    private static async Task HandleCreateAsync(HttpContext context)
    {
        // This middleware runs before the application's own authentication
        // middleware, so populate the principal explicitly from the identity
        // application cookie — the same cookie the sign in form issued.
        AuthenticateResult authenticationResult =
            await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        if (authenticationResult.Succeeded && authenticationResult.Principal is not null)
        {
            context.User = authenticationResult.Principal;
        }

        SpreadsheetUpload? currentUpload = await FindCurrentUploadAsync(context);

        if (currentUpload is null)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsync("No spreadsheet is uploaded.");
            return;
        }

        SpreadsheetDocument document = BuildDocument(["GAT %S"]);
        string actingUserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        AntibioticAbbreviationService abbreviationService =
            context.RequestServices.GetRequiredService<AntibioticAbbreviationService>();

        WriteResult result = await abbreviationService.CreateMappingAsync(
            currentUpload.Id,
            "GAT %S",
            "Gatifloxacin",
            actingUserId,
            context.User,
            document);

        string claimDump = string.Join(
            ",",
            context.User.Claims.Select(claim => $"{claim.Type}={claim.Value}"));
        await File.AppendAllTextAsync(
            "/tmp/endpoint-diag.txt",
            $"result={result.Succeeded} claims={claimDump}{Environment.NewLine}");

        context.Response.StatusCode = result.Succeeded
            ? StatusCodes.Status200OK
            : StatusCodes.Status403Forbidden;

        await context.Response.WriteAsync(result.Message);
    }

    private static async Task<SpreadsheetUpload?> FindCurrentUploadAsync(HttpContext context)
    {
        ApplicationDbContext database =
            context.RequestServices.GetRequiredService<ApplicationDbContext>();

        SpreadsheetUpload? currentUpload = await database.SpreadsheetUploads
            .AsNoTracking()
            .OrderBy(upload => upload.Id)
            .FirstOrDefaultAsync();

        return currentUpload;
    }

    private static SpreadsheetDocument BuildDocument(IReadOnlyList<string> antibioticNames)
    {
        SpreadsheetDocument document = new(
            originalFileName: "test.csv",
            fileFormat: SpreadsheetFileFormat.Csv,
            orientation: SpreadsheetOrientation.AntibioticsAsRows,
            organismNames: ["Escherichia coli"],
            antibioticNames: antibioticNames,
            measurements: []);

        return document;
    }
}
