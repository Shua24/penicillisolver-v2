using System.Net;
using System.Text.RegularExpressions;

using penicillisolver_v2.Domain.Constants;

namespace PenicilliSolver.IntegrationTests;

/// <summary>Checks culture selection and its redirect through the real HTTP pipeline.</summary>
public sealed class CultureSelectionTests : IClassFixture<PenicilliSolverApplicationFactory>
{
    private readonly PenicilliSolverApplicationFactory factory;

    public CultureSelectionTests(PenicilliSolverApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("https://localhost/spreadsheet?organism=test#ranking", "/spreadsheet?organism=test")]
    [InlineData("https://LOCALHOST:443/Account/Login", "/Account/Login")]
    [InlineData("https://localhost/", "/")]
    [InlineData("https://example.test/spreadsheet", "/")]
    [InlineData("http://localhost/spreadsheet", "/")]
    [InlineData("https://localhost:444/spreadsheet", "/")]
    [InlineData("https://localhost//example.test/path", "/")]
    [InlineData("https://localhost/\\example.test/path", "/")]
    [InlineData("//example.test/path", "/")]
    [InlineData("/spreadsheet", "/")]
    [InlineData("invalid referer", "/")]
    [InlineData(null, "/")]
    public async Task Redirect_requires_a_same_origin_referer_with_a_local_path(
        string? referer,
        string expectedPath)
    {
        using HttpClient client = CreateClient();
        using HttpResponseMessage response = await SelectCultureAsync(client, referer);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expectedPath, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Indonesian_cookie_selects_Indonesian_without_changing_another_clients_English_default()
    {
        using HttpClient indonesianClient = CreateClient();
        using HttpClient englishClient = CreateClient();

        using HttpResponseMessage selection = await SelectCultureAsync(indonesianClient, null);
        Assert.Equal(HttpStatusCode.Redirect, selection.StatusCode);

        string indonesianPage = await indonesianClient.GetStringAsync("/Account/Login");
        string englishPage = await englishClient.GetStringAsync("/Account/Login");

        Assert.Contains("Gunakan akun lokal untuk masuk.", indonesianPage, StringComparison.Ordinal);
        Assert.Contains("Use a local account to log in.", englishPage, StringComparison.Ordinal);
    }

    private HttpClient CreateClient()
    {
        HttpClient client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        return client;
    }

    private static async Task<HttpResponseMessage> SelectCultureAsync(HttpClient client, string? referer)
    {
        string formHtml = await client.GetStringAsync("/Account/Login");
        Match tokenMatch = Regex.Match(
            formHtml,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<value>[^\"]*)\"");
        Assert.True(tokenMatch.Success, "The form must supply an antiforgery token.");

        using HttpRequestMessage request = new(HttpMethod.Post, "/culture/set");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [LanguagePreference.FormFieldName] = SupportedLanguages.Indonesian,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(tokenMatch.Groups["value"].Value),
        });

        if (referer is not null)
        {
            request.Headers.TryAddWithoutValidation("Referer", referer);
        }

        HttpResponseMessage response = await client.SendAsync(request);
        return response;
    }
}
