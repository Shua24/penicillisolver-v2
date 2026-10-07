using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.ValueObjects;
using penicillisolver_v2.Resources;

namespace penicillisolver_v2.Services;

/// <summary>
/// The whole API for reading and writing antibiotic abbreviation mappings.
/// </summary>
/// <remarks>
/// Every method is scoped to a spreadsheet upload. There is deliberately no
/// method that resolves an abbreviation without an upload identifier, because
/// the same abbreviation string may mean different drugs in different files;
/// a lookup that ignored the upload would have to guess.
/// <para>
/// Write authorization is enforced HERE, on the server, by resolving the
/// <c>CanManageAntibioticMappings</c> policy through the injected
/// <see cref="IAuthorizationService"/>. The page also hides the controls behind
/// the same policy, but hiding a control is not enforcement: a caller reaching
/// this service directly is rejected the same way.
/// </para>
/// </remarks>
public sealed partial class AntibioticAbbreviationService
{
    private readonly ApplicationDbContext database;
    private readonly IAuthorizationService authorizationService;

    /// <summary>
    /// The localizer for this service's own messages. Built from the factory
    /// because the service is not a Razor component.
    /// </summary>
    private readonly IStringLocalizer localizer;

    /// <summary>Creates the service over the application database.</summary>
    public AntibioticAbbreviationService(
        ApplicationDbContext database,
        IAuthorizationService authorizationService,
        IStringLocalizerFactory localizerFactory)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(localizerFactory);

        this.database = database;
        this.authorizationService = authorizationService;
        this.localizer = localizerFactory.Create(typeof(SharedResource));
    }

    /// <summary>
    /// Creates one mapping for an upload. The abbreviation must actually exist
    /// in that upload's document, and a duplicate abbreviation for the same
    /// upload is refused.
    /// </summary>
    public async Task<WriteResult> CreateMappingAsync(
        int spreadsheetUploadId,
        string abbreviation,
        string fullName,
        string actingUserId,
        ClaimsPrincipal actingPrincipal,
        SpreadsheetDocument document)
    {
        ArgumentNullException.ThrowIfNull(actingPrincipal);
        ArgumentNullException.ThrowIfNull(document);

        WriteResult authorizationResult = await EnsureMayManageAsync(actingPrincipal);
        if (!authorizationResult.Succeeded)
        {
            return authorizationResult;
        }

        string trimmedAbbreviation = NormaliseOrDefault(abbreviation);
        string trimmedFullName = NormaliseOrDefault(fullName);

        if (trimmedAbbreviation.Length == 0)
        {
            return WriteResult.Failure(localizer["Service_MappingEnterAbbreviation"]);
        }

        if (trimmedFullName.Length == 0)
        {
            return WriteResult.Failure(localizer["Service_MappingEnterFullName"]);
        }

        bool abbreviationExists = document.AntibioticNames.Any(name =>
            string.Equals(NormaliseOrDefault(name), trimmedAbbreviation, StringComparison.Ordinal));

        if (!abbreviationExists)
        {
            return WriteResult.Failure(
                localizer["Service_MappingAbbreviationNotInSpreadsheet", trimmedAbbreviation]);
        }

        bool alreadyMapped = await database.AntibioticAbbreviations
            .AsNoTracking()
            .AnyAsync(row =>
                row.SpreadsheetUploadId == spreadsheetUploadId
                && row.Abbreviation == trimmedAbbreviation);

        if (alreadyMapped)
        {
            return WriteResult.Failure(
                localizer["Service_MappingAlreadyMapped", trimmedAbbreviation]);
        }

        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        AntibioticAbbreviation mapping = new()
        {
            SpreadsheetUploadId = spreadsheetUploadId,
            Abbreviation = trimmedAbbreviation,
            FullName = trimmedFullName,
            CreatedByUserId = actingUserId,
            CreatedAtUtc = timestamp,
            LastModifiedByUserId = actingUserId,
            LastModifiedAtUtc = timestamp,
        };

        database.AntibioticAbbreviations.Add(mapping);
        int writtenRowCount = await database.SaveChangesAsync();

        if (writtenRowCount == 0)
        {
            return WriteResult.Failure(localizer["Service_MappingCouldNotBeSaved"]);
        }

        return WriteResult.Success(
            localizer["Service_MappingCreated", trimmedAbbreviation, trimmedFullName],
            mapping.Id);
    }

    /// <summary>Changes the full name of an existing mapping.</summary>
    public async Task<WriteResult> UpdateMappingAsync(
        int mappingId,
        string fullName,
        string actingUserId,
        ClaimsPrincipal actingPrincipal)
    {
        ArgumentNullException.ThrowIfNull(actingPrincipal);

        WriteResult authorizationResult = await EnsureMayManageAsync(actingPrincipal);
        if (!authorizationResult.Succeeded)
        {
            return authorizationResult;
        }

        string trimmedFullName = NormaliseOrDefault(fullName);

        if (trimmedFullName.Length == 0)
        {
            return WriteResult.Failure(localizer["Service_MappingEnterFullName"]);
        }

        AntibioticAbbreviation? mapping = await database.AntibioticAbbreviations
            .FirstOrDefaultAsync(row => row.Id == mappingId);

        if (mapping is null)
        {
            return WriteResult.Failure(localizer["Service_MappingNoLongerExists"]);
        }

        mapping.FullName = trimmedFullName;
        mapping.LastModifiedByUserId = actingUserId;
        mapping.LastModifiedAtUtc = DateTimeOffset.UtcNow;

        int writtenRowCount = await database.SaveChangesAsync();

        if (writtenRowCount == 0)
        {
            return WriteResult.Failure(localizer["Service_MappingCouldNotBeSaved"]);
        }

        return WriteResult.Success(
            localizer["Service_MappingUpdated", mapping.Abbreviation, trimmedFullName],
            mapping.Id);
    }

    /// <summary>Removes an existing mapping.</summary>
    public async Task<WriteResult> DeleteMappingAsync(
        int mappingId,
        string actingUserId,
        ClaimsPrincipal actingPrincipal)
    {
        ArgumentNullException.ThrowIfNull(actingPrincipal);

        WriteResult authorizationResult = await EnsureMayManageAsync(actingPrincipal);
        if (!authorizationResult.Succeeded)
        {
            return authorizationResult;
        }

        AntibioticAbbreviation? mapping = await database.AntibioticAbbreviations
            .FirstOrDefaultAsync(row => row.Id == mappingId);

        if (mapping is null)
        {
            return WriteResult.Failure(localizer["Service_MappingNoLongerExists"]);
        }

        string abbreviation = mapping.Abbreviation;

        database.AntibioticAbbreviations.Remove(mapping);
        int writtenRowCount = await database.SaveChangesAsync();

        if (writtenRowCount == 0)
        {
            return WriteResult.Failure(localizer["Service_MappingCouldNotBeRemoved"]);
        }

        return WriteResult.Success(localizer["Service_MappingRemoved", abbreviation]);
    }

    /// <summary>
    /// Reports whether the principal may manage mappings, or a failure carrying
    /// the message to show. This is the single server-side gate every write
    /// passes through.
    /// </summary>
    private async Task<WriteResult> EnsureMayManageAsync(ClaimsPrincipal actingPrincipal)
    {
        AuthorizationResult authorizationResult = await authorizationService.AuthorizeAsync(
            actingPrincipal,
            resource: null,
            policyName: AuthorizationPolicyNames.CanManageAntibioticMappings);

        if (authorizationResult.Succeeded)
        {
            return WriteResult.Success("Allowed.");
        }

        // Deliberately English: names a role. Role names are a UI display
        // concern, localised at the Razor call site, not here.
        return WriteResult.Failure(
            "Only a clinical pathologist may create, edit, or delete antibiotic mappings.");
    }
}
