// Delete-confirmation modal for the /antibiotic-mappings page.
//
// This module is a .razor.js isolated asset: the browser executes it ONCE, at
// app startup, as part of the component JS bundle. That can happen BEFORE any
// client-side navigation reaches this page, so the <dialog> element may not
// exist yet when the module runs. We therefore NEVER cache the element at load;
// every entry point does a fresh document lookup so the wiring works no matter
// which page the user is on when the module executes.
//
// C# <-> JS contract (all on window.antibioticMappingDeleteDialog):
//   init(reference)  - from OnAfterRender(firstRender), once the page's own
//                      <dialog> is in the DOM. Captures the DotNetObjectReference
//                      and relays the NATIVE "close" event (Esc / backdrop click)
//                      back into C# - the path the confirm/cancel buttons, which
//                      are Blazor @onclick handlers, do not trigger.
//   open()           - showModal() the pending-row confirmation.
//   close()          - close it (used by cancel + confirm in C#).

const deleteDialogId = "antibiotic-mapping-delete-dialog";
let closeCallbackReference;

/**
 * Looks up the delete-confirmation dialog element fresh from the DOM, since
 * this module runs once at startup and the element may not exist yet.
 * @returns {HTMLDialogElement|null} The dialog element, or null if it is not currently in the DOM.
 */
function findDialog() {
    return document.getElementById(deleteDialogId);
}

window.antibioticMappingDeleteDialog = {
    /**
     * Captures the DotNetObjectReference used to relay native dialog closes
     * back into C#, and wires the native "close" event listener exactly once.
     * @param {object} reference - The DotNetObjectReference for the page's AntibioticMapping component.
     */
    init(reference) {
        closeCallbackReference = reference;

        // Wire the native-close relay exactly once, when the element is known to
        // exist (this runs from the page's first interactive render).
        const dialog = findDialog();
        if (dialog !== null && !dialog.hasAttribute("data-antibiotic-mapping-wired")) {
            dialog.setAttribute("data-antibiotic-mapping-wired", "true");
            dialog.addEventListener("close", handleNativeDialogClose);
        }
    },

    /**
     * Opens the delete-confirmation dialog if it exists and is not already open.
     */
    open() {
        const dialog = findDialog();
        if (dialog !== null && !dialog.open) {
            dialog.showModal();
        }
    },

    /**
     * Closes the delete-confirmation dialog if it exists and is open.
     */
    close() {
        const dialog = findDialog();
        if (dialog !== null && dialog.open) {
            dialog.close();
        }
    },
};

/**
 * Relays a native dialog close (Esc key or backdrop click) back into C# via
 * the captured DotNetObjectReference, if one has been set.
 */
function handleNativeDialogClose() {
    if (closeCallbackReference !== undefined && closeCallbackReference !== null) {
        closeCallbackReference.SyncNativeDialogClose();
    }
}
