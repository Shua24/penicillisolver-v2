using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.ValueObjects;
using penicillisolver_v2.Services;

namespace penicillisolver_v2.Components.Pages;

/// <summary>
/// The behaviour behind the <c>/antibiotic-mappings</c> page. Every action is
/// scoped to the CURRENT upload: the page never maps an abbreviation without
/// knowing which file it belongs to.
/// </summary>
public partial class AntibioticMapping
{
    private readonly CreateMappingInput createInput = new();

    private SpreadsheetUpload? currentUpload;
    private SpreadsheetDocument? currentDocument;
    private IReadOnlyList<AntibioticAbbreviation> mappings = [];
    private IReadOnlyList<string> unmappedAbbreviations = [];
    private ClaimsPrincipal? actingPrincipal;

    private bool isLoading = true;
    private bool canManageMappings;
    private string? statusMessage;
    private bool statusSucceeded;

    private int? editingMappingId;
    private string editingFullName = string.Empty;
    private AntibioticAbbreviation? pendingDelete;

    /// <summary>
    /// True when the current file uses complete antibiotic names, so there is
    /// nothing for a pathologist to interpret.
    /// </summary>
    /// <remarks>
    /// The csv sample is the motivating case: it lists "Amoxicillin" and
    /// "Amoxicillin/Clavulanic acid" outright. Offering to map those would ask
    /// the user to re-enter a name the file already spells out, so the whole
    /// workflow is withheld and the page explains why instead.
    /// </remarks>
    private bool mappingIsNotApplicable =>
        currentDocument is not null
        && AntibioticNameClassifier.NeedsNoMapping(currentDocument.AntibioticNames);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        AuthenticationState authenticationState =
            await AuthenticationStateProvider.GetAuthenticationStateAsync();
        actingPrincipal = authenticationState.User;

        AuthorizationResult authorizationResult = await AuthorizationService.AuthorizeAsync(
            actingPrincipal,
            resource: null,
            policyName: AuthorizationPolicyNames.CanManageAntibioticMappings);
        canManageMappings = authorizationResult.Succeeded;

        currentUpload = await QueryService.GetCurrentUploadAsync();

        if (currentUpload is not null)
        {
            currentDocument = await QueryService.GetCurrentDocumentAsync();
            mappings = await AbbreviationService.GetMappingsAsync(currentUpload.Id);
        }

        await ReloadUnmappedAbbreviationsAsync();
        isLoading = false;
    }

    private async Task CreateMappingAsync()
    {
        if (currentUpload is null || currentDocument is null || actingPrincipal is null)
        {
            return;
        }

        string actingUserId = ResolveActingUserId();

        WriteResult result = await AbbreviationService.CreateMappingAsync(
            currentUpload.Id,
            createInput.Abbreviation,
            createInput.FullName,
            actingUserId,
            actingPrincipal,
            currentDocument);

        ApplyResult(result);

        if (result.Succeeded)
        {
            createInput.Abbreviation = string.Empty;
            createInput.FullName = string.Empty;
        }

        await ReloadMappingsAsync();
    }

    private void BeginEdit(AntibioticAbbreviation mapping)
    {
        editingMappingId = mapping.Id;
        editingFullName = mapping.FullName;
        statusMessage = null;
    }

    private void CancelEdit()
    {
        editingMappingId = null;
        editingFullName = string.Empty;
    }

    private async Task SaveEditAsync(int mappingId)
    {
        if (actingPrincipal is null)
        {
            return;
        }

        string actingUserId = ResolveActingUserId();

        WriteResult result = await AbbreviationService.UpdateMappingAsync(
            mappingId,
            editingFullName,
            actingUserId,
            actingPrincipal);

        ApplyResult(result);

        if (result.Succeeded)
        {
            CancelEdit();
        }

        await ReloadMappingsAsync();
    }

    private void RequestDelete(AntibioticAbbreviation mapping)
    {
        pendingDelete = mapping;
        statusMessage = null;
    }

    private async Task ConfirmDeleteAsync()
    {
        if (pendingDelete is null || actingPrincipal is null)
        {
            return;
        }

        string actingUserId = ResolveActingUserId();

        WriteResult result = await AbbreviationService.DeleteMappingAsync(
            pendingDelete.Id,
            actingUserId,
            actingPrincipal);

        ApplyResult(result);
        pendingDelete = null;

        await ReloadMappingsAsync();
    }

    private string ResolveActingUserId()
    {
        string? userId = actingPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return userId ?? string.Empty;
    }

    private void ApplyResult(WriteResult result)
    {
        statusSucceeded = result.Succeeded;
        statusMessage = result.Message;
    }

    private async Task ReloadMappingsAsync()
    {
        if (currentUpload is null)
        {
            return;
        }

        mappings = await AbbreviationService.GetMappingsAsync(currentUpload.Id);
        await ReloadUnmappedAbbreviationsAsync();
    }

    private async Task ReloadUnmappedAbbreviationsAsync()
    {
        if (currentUpload is null || currentDocument is null)
        {
            return;
        }

        unmappedAbbreviations = await AbbreviationService.GetUnmappedAbbreviationsAsync(
            currentUpload.Id,
            currentDocument);
    }
}
