// Delete-confirmation modal for the /antibiotic-mappings page.
//
// This collocated module is loaded by the page's C# partial with an awaited
// dynamic import (IJSRuntime "import"), so every export below is in scope the
// moment C# calls it - no <script> tag, no document-load-order race. Nothing
// here may be captured at module load: the <dialog> element only exists while
// the page is rendered, so every entry point looks it up fresh.
//
// C# <-> JS contract:
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
 * the element only exists while the page is rendered.
 * @returns {HTMLDialogElement|null} The dialog element, or null if it is not currently in the DOM.
 */
function findDialog() {
    return document.getElementById(deleteDialogId);
}

/**
 * Captures the DotNetObjectReference used to relay native dialog closes
 * back into C#, and wires the native "close" event listener exactly once.
 * @param {object} reference - The DotNetObjectReference for the page's AntibioticMapping component.
 */
export function init(reference) {
    closeCallbackReference = reference;

    // Wire the native-close relay exactly once, when the element is known to
    // exist (this runs from the page's first interactive render).
    const dialog = findDialog();
    if (dialog !== null && !dialog.hasAttribute("data-antibiotic-mapping-wired")) {
        dialog.setAttribute("data-antibiotic-mapping-wired", "true");
        dialog.addEventListener("close", handleNativeDialogClose);
    }
}

/**
 * Opens the delete-confirmation dialog if it exists and is not already open.
 */
export function open() {
    const dialog = findDialog();
    if (dialog !== null && !dialog.open) {
        dialog.showModal();
    }
}

/**
 * Closes the delete-confirmation dialog if it exists and is open.
 */
export function close() {
    const dialog = findDialog();
    if (dialog !== null && dialog.open) {
        dialog.close();
    }
}

/**
 * Relays a native dialog close (Esc key or backdrop click) back into C# via
 * the captured DotNetObjectReference, if one has been set.
 */
function handleNativeDialogClose() {
    if (closeCallbackReference !== undefined && closeCallbackReference !== null) {
        closeCallbackReference.SyncNativeDialogClose();
    }
}
