using System.Net;
using System.Text.RegularExpressions;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// Posts an antiforgery protected Blazor form the way a browser would: fetch
/// the form page, read the hidden verification token, then post it back with
/// the handler marker.
/// </summary>
internal sealed partial class AntiforgeryFormHelper
{
    private readonly HttpClient client;

    /// <summary>Creates the helper over a cookie enabled client.</summary>
    public AntiforgeryFormHelper(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    /// <summary>Fetches the form and posts the supplied values to it.</summary>
    public async Task<HttpResponseMessage> PostFormAsync(
        string formPath,
        string formName,
        IReadOnlyDictionary<string, string> formValues)
    {
        HttpResponseMessage formResponse = await client.GetAsync(formPath);
        string formHtml = await formResponse.Content.ReadAsStringAsync();

        string? antiforgeryToken = ExtractAntiforgeryToken(formHtml);
        if (antiforgeryToken is null)
        {
            throw new InvalidOperationException(
                $"No antiforgery token was found on '{formPath}'.");
        }

        List<KeyValuePair<string, string>> postedValues =
        [
            new("__RequestVerificationToken", antiforgeryToken),
            new("_handler", formName),
        ];

        foreach (KeyValuePair<string, string> pair in formValues)
        {
            postedValues.Add(pair);
        }

        FormUrlEncodedContent content = new(postedValues);

        return await client.PostAsync(formPath, content);
    }

    private static string? ExtractAntiforgeryToken(string html)
    {
        Match match = AntiforgeryTokenPattern().Match(html);
        if (match.Success)
        {
            return match.Groups["value"].Value;
        }

        Match reversedMatch = ReversedAntiforgeryTokenPattern().Match(html);
        return reversedMatch.Success ? reversedMatch.Groups["value"].Value : null;
    }

    [GeneratedRegex(
        "name=\"__RequestVerificationToken\"[^>]*value=\"(?<value>[^\"]*)\"",
        RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryTokenPattern();

    [GeneratedRegex(
        "value=\"(?<value>[^\"]*)\"[^>]*name=\"__RequestVerificationToken\"",
        RegexOptions.IgnoreCase)]
    private static partial Regex ReversedAntiforgeryTokenPattern();
}
