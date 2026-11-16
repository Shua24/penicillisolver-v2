using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

using penicillisolver_v2.Domain.Constants;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// Proves that a field validation failure reaches a reader in the reader's
/// language, through the application's real form pipeline, and exactly once.
/// </summary>
/// <remarks>
/// The unit tests drive the attributes directly; this drives an actual POST so
/// the whole chain is covered: the language cookie, request localisation, the
/// Blazor edit context handing its service provider to each
/// <see cref="ValidationContext"/>, and the message rendered beside the field.
/// The expected text is read from the resource file, so a translation rewrite
/// does not invalidate the test.
/// </remarks>
public sealed class LocalizedValidationMessageTests : IClassFixture<PenicilliSolverApplicationFactory>
{
    private readonly PenicilliSolverApplicationFactory factory;

    /// <summary>Creates the test over the shared application factory.</summary>
    public LocalizedValidationMessageTests(PenicilliSolverApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        this.factory = factory;
    }

    /// <summary>
    /// Verifies that an empty email field is refused with the Indonesian
    /// sentence, naming the field in Indonesian too.
    /// </summary>
    [Fact]
    public async Task The_registration_page_refuses_an_empty_email_in_indonesian()
    {
        IReadOnlyDictionary<string, string> resources =
            TestResourceFiles.LoadValues(TestResourceFiles.IndonesianFileName);

        string expectedMessage = string.Format(
            new CultureInfo(SupportedLanguages.Indonesian),
            resources["Validation_Required"],
            resources["Field_Email"]);

        // What the framework would say if the attribute had not been replaced.
        // Derived rather than written out, so this does not become a copy of
        // the English translation either.
        string frameworkMessage = new RequiredAttribute().FormatErrorMessage("Email");

        string renderedText = await PostRegistrationAsync(new Dictionary<string, string>
        {
            ["Input.DisplayName"] = "Validation Page Probe",
            ["Input.Email"] = string.Empty,
            ["Input.RequestedRole"] = ApplicationRoleNames.OtherDoctor,
            ["Input.Password"] = "IntegrationTest1!",
            ["Input.ConfirmPassword"] = "IntegrationTest1!",
        });

        Assert.Contains(expectedMessage, renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain(frameworkMessage, renderedText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a failed field renders its message exactly once.
    /// </summary>
    /// <remarks>
    /// The page used to carry a <c>ValidationSummary</c> with no <c>Model</c>
    /// parameter, which renders every message in the store, while each field
    /// also rendered its own. Every error therefore appeared twice: once at the
    /// top of the form and once beside the field. This test is what keeps the
    /// summary from coming back.
    /// </remarks>
    [Fact]
    public async Task A_failed_field_renders_its_message_exactly_once()
    {
        IReadOnlyDictionary<string, string> resources =
            TestResourceFiles.LoadValues(TestResourceFiles.IndonesianFileName);

        string comparisonMessage = string.Format(
            new CultureInfo(SupportedLanguages.Indonesian),
            resources["Validation_Compare"],
            resources["Field_Password"],
            resources["Field_ConfirmPassword"]);

        string requiredMessage = string.Format(
            new CultureInfo(SupportedLanguages.Indonesian),
            resources["Validation_Required"],
            resources["Field_Email"]);

        string mismatchText = await PostRegistrationAsync(new Dictionary<string, string>
        {
            ["Input.DisplayName"] = "Duplicate Check Probe",
            ["Input.Email"] = $"duplicate.check.{Guid.NewGuid():N}@example.test",
            ["Input.RequestedRole"] = ApplicationRoleNames.OtherDoctor,
            ["Input.Password"] = "IntegrationTest1!",
            ["Input.ConfirmPassword"] = "Different1!",
        });

        Assert.Equal(1, CountOccurrences(mismatchText, comparisonMessage));

        string requiredText = await PostRegistrationAsync(new Dictionary<string, string>
        {
            ["Input.DisplayName"] = "Required Check Probe",
            ["Input.Email"] = string.Empty,
            ["Input.RequestedRole"] = ApplicationRoleNames.OtherDoctor,
            ["Input.Password"] = "IntegrationTest1!",
            ["Input.ConfirmPassword"] = "IntegrationTest1!",
        });

        Assert.Equal(1, CountOccurrences(requiredText, requiredMessage));
    }

    /// <summary>
    /// Posts the registration form under Indonesian and returns the rendered
    /// page as text.
    /// </summary>
    /// <param name="formValues">The field values to submit.</param>
    /// <returns>The response body, HTML-decoded.</returns>
    private async Task<string> PostRegistrationAsync(Dictionary<string, string> formValues)
    {
        using HttpClient client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        await SelectIndonesianAsync(client);

        AntiforgeryFormHelper formHelper = new(client);

        using HttpResponseMessage registerResponse = await formHelper.PostFormAsync(
            "/Account/Register",
            "register",
            formValues);

        string pageHtml = await registerResponse.Content.ReadAsStringAsync();

        // The rendered sentence is compared in decoded form: Blazor escapes
        // characters in the text it writes.
        string renderedText = WebUtility.HtmlDecode(pageHtml);

        return renderedText;
    }

    /// <summary>Counts how many times a message appears in the rendered page.</summary>
    private static int CountOccurrences(string renderedText, string message)
    {
        int occurrences = Regex.Matches(renderedText, Regex.Escape(message)).Count;

        return occurrences;
    }

    /// <summary>
    /// Posts the language switcher the way the browser does, which is what sets
    /// the culture cookie the request pipeline reads.
    /// </summary>
    private static async Task SelectIndonesianAsync(HttpClient client)
    {
        string loginHtml = await client.GetStringAsync("/Account/Login");

        Match tokenMatch = Regex.Match(
            loginHtml,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<value>[^\"]*)\"");

        Assert.True(tokenMatch.Success, "The login form must supply an antiforgery token.");

        using HttpRequestMessage request = new(HttpMethod.Post, "/culture/set");

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [LanguagePreference.FormFieldName] = SupportedLanguages.Indonesian,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(tokenMatch.Groups["value"].Value),
        });

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
