using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using penicillisolver_v2.Services;

namespace penicillisolver_v2.Components.Pages;

/// <summary>
/// The delete-confirmation <c>&lt;dialog&gt;</c> for the <c>/antibiotic-mappings</c>
/// page. The dialog lives at the page's root so its node is present for the
/// scoped module at first render; only THIS partial owns its open/close state
/// machine, which the main page partial triggers through <c>deleteDialogOpen</c>
/// and the scoped module relays back through <c>SyncNativeDialogClose</c>.
/// </summary>
public partial class AntibioticMapping
{
    private AntibioticMappingRow? pendingDelete;
    private bool deleteDialogOpen;

    private void RequestDelete(AntibioticMappingRow row)
    {
        pendingDelete = row;
        statusMessage = null;
        deleteDialogOpen = true;

        // The actual showModal() happens in OnAfterRenderAsync: the re-render
        // must flush this pending row into the dialog's text first.
    }

    private void CancelDelete()
    {
        pendingDelete = null;
        deleteDialogOpen = false;

        _ = JSRuntime.InvokeVoidAsync("antibioticMappingDeleteDialog.close");
    }

    /// <summary>
    /// Invoked by the scoped module when the dialog closes natively (Esc key
    /// or a backdrop click), which the confirm / cancel buttons do not cause.
    /// Clears the pending row so the next Remove click opens a clean dialog.
    /// </summary>
    [JSInvokable]
    public void SyncNativeDialogClose()
    {
        if (deleteDialogOpen)
        {
            pendingDelete = null;
            deleteDialogOpen = false;
        }
    }

    private async Task ConfirmDeleteAsync()
    {
        if (pendingDelete is null || actingPrincipal is null)
        {
            return;
        }

        AntibioticMappingRow row = pendingDelete;
        pendingDelete = null;
        deleteDialogOpen = false;

        _ = JSRuntime.InvokeVoidAsync("antibioticMappingDeleteDialog.close");

        if (row.Mapping is null)
        {
            statusSucceeded = false;
            statusMessage = Localizer["Service_MappingNothingToRemove"];
            return;
        }

        string actingUserId = ResolveActingUserId();

        WriteResult result = await AbbreviationService.DeleteMappingAsync(
            row.Mapping.Id,
            actingUserId,
            actingPrincipal);

        statusSucceeded = result.Succeeded;
        statusMessage = result.Message;

        if (result.Succeeded)
        {
            await ReloadMappingRowsAsync();
        }
    }
}
