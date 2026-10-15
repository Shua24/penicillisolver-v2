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
public partial class AntibioticMapping : IAsyncDisposable
{
    private AntibioticMappingRow? pendingDelete;
    private bool deleteDialogOpen;

    private IJSObjectReference? deleteDialogModule;
    private DotNetObjectReference<AntibioticMapping>? deleteDialogReference;

    /// <summary>
    /// Loads the page's collocated dialog module with an awaited dynamic
    /// import, then hands it the .NET reference it uses to relay native
    /// dialog closes back into C#. The await is the point: unlike a
    /// <c>&lt;script&gt;</c> tag, it guarantees the module's functions are in
    /// scope before the first call, whatever the document load order was.
    /// </summary>
    private async Task InitialiseDeleteDialogAsync()
    {
        deleteDialogModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./Components/Pages/AntibioticMapping.razor.js");

        deleteDialogReference = DotNetObjectReference.Create(this);

        await deleteDialogModule.InvokeVoidAsync("init", deleteDialogReference);
    }

    /// <summary>
    /// Opens the confirmation dialog for the pending row.
    /// </summary>
    private async Task OpenDeleteDialogAsync()
    {
        if (deleteDialogModule is not null)
        {
            await deleteDialogModule.InvokeVoidAsync("open");
        }
    }

    /// <summary>
    /// Closes the confirmation dialog.
    /// </summary>
    private async Task CloseDeleteDialogAsync()
    {
        if (deleteDialogModule is not null)
        {
            await deleteDialogModule.InvokeVoidAsync("close");
        }
    }

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

        _ = CloseDeleteDialogAsync();
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

        _ = CloseDeleteDialogAsync();

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

    /// <summary>
    /// Releases the .NET reference and the JS module reference the page holds
    /// for the dialog.
    /// </summary>
    /// <returns>A task that completes once both references are released.</returns>
    public async ValueTask DisposeAsync()
    {
        deleteDialogReference?.Dispose();

        if (deleteDialogModule is not null)
        {
            try
            {
                await deleteDialogModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is already gone; there is nothing left to
                // release on the server.
            }
        }
    }
}
