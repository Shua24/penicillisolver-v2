using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

using penicillisolver_v2.Domain.Constants;

namespace penicillisolver_v2.Components.Layout;

/// <summary>
/// Applies the culture chosen in the language switcher to interactive Blazor
/// circuits, and re-applies it after a client side navigation.
/// </summary>
/// <remarks>
/// <para>
/// <c>UseRequestLocalization</c> only sets the culture for an HTTP request. An
/// interactive server circuit is a long lived SignalR connection with no HTTP
/// request behind each render, so <see cref="CultureInfo.CurrentUICulture"/>
/// inside an <c>@rendermode InteractiveServer</c> component does NOT follow the
/// cookie on its own. Without this component the static first paint would be
/// Indonesian while every interactive update reverted to the host default.
/// </para>
/// <para>
/// The component reads the culture from the SAME cookie the request pipeline
/// uses (through the browser, so the value is the one the user actually chose)
/// and pins it on the current thread for the life of the circuit. It re-reads
/// after an enhanced navigation so switching language updates the circuit
/// without a full page reload.
/// </para>
/// </remarks>
public sealed partial class CultureProvider : ComponentBase, IDisposable
{
    [Inject]
    private IJSRuntime JavaScriptRuntime { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        NavigationManager.LocationChanged += OnLocationChanged;

        await ApplyStoredCultureAsync();
    }

    private async Task ApplyStoredCultureAsync()
    {
        string? storedCulture = await ReadCultureCookieAsync();

        string cultureToApply = IsSupported(storedCulture)
            ? storedCulture!
            : SupportedLanguages.Default;

        CultureInfo culture = new(cultureToApply);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private async Task<string?> ReadCultureCookieAsync()
    {
        // The framework's cookie provider stores "c=<culture>|uic=<culture>".
        // Only the culture half is needed here: the application does not carry a
        // separate formatting culture.
        IJSObjectReference module = await JavaScriptRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "./js/culture-cookie-reader.js");

        string cookieValue = await module.InvokeAsync<string>(
            "read",
            LanguagePreference.CookieName);

        if (string.IsNullOrWhiteSpace(cookieValue))
        {
            return null;
        }

        string[] segments = cookieValue.Split('|');

        foreach (string segment in segments)
        {
            string trimmedSegment = segment.Trim();

            if (!trimmedSegment.StartsWith("c=", StringComparison.Ordinal))
            {
                continue;
            }

            return trimmedSegment[2..];
        }

        return null;
    }

    private static bool IsSupported(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return false;
        }

        bool matchesTag = SupportedLanguages.All.Contains(culture, StringComparer.Ordinal);

        bool matchesTwoLetter = SupportedLanguages.All.Any(configured =>
            configured.StartsWith(culture, StringComparison.OrdinalIgnoreCase)
            || culture.StartsWith(
                configured[..2],
                StringComparison.OrdinalIgnoreCase));

        return matchesTag || matchesTwoLetter;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs eventArgs)
    {
        // A navigation is a good moment to re-read the cookie: the switcher may
        // have just changed it. The discard is deliberate for an event handler,
        // which the framework cannot await.
        _ = ApplyStoredCultureAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
    }
}
