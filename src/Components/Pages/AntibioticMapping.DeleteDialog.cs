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

    /// <summary>
    /// Begins the delete flow for a row: remembers it as pending and opens the
    /// confirmation dialog on the next render.
    /// </summary>
    /// <param name="row">The mapping row the user asked to remove.</param>
    private void RequestDelete(AntibioticMappingRow row)
    {
        pendingDelete = row;
        statusMessage = null;
        deleteDialogOpen = true;

        // The actual showModal() happens in OnAfterRenderAsync: the re-render
        // must flush this pending row into the dialog's text first.
    }

    /// <summary>
    /// Abandons the pending delete and closes the dialog without making any
    /// change.
    /// </summary>
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

    /// <summary>
    /// Deletes the pending mapping's meaning and refreshes the rows on success.
    /// </summary>
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
            statusMessage = "That abbreviation has no meaning to remove.";
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
