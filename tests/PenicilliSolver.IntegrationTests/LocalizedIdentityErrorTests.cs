using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// Proves that an identity failure reaches a reader in the reader's language,
/// through the application's real container and real HTTP pipeline.
/// </summary>
/// <remarks>
/// These tests exist because the previous behaviour was invisible to code
/// review: every call site read <c>error.Description</c> faithfully, and the
/// English sentence came from the framework's own describer. Only running a real
/// failure shows whether the replacement is wired in, so both tests drive one.
/// The expected text is read from the resource file rather than written out
/// here, so a translation rewrite does not invalidate the test.
/// </remarks>
public sealed class LocalizedIdentityErrorTests : IClassFixture<PenicilliSolverApplicationFactory>
{
    /// <summary>The error the weak password below is guaranteed to trip.</summary>
    private const string ExpectedErrorKey = "Identity_PasswordRequiresUpper";

    private readonly PenicilliSolverApplicationFactory factory;

    /// <summary>Creates the test over the shared application factory.</summary>
    public LocalizedIdentityErrorTests(PenicilliSolverApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        this.factory = factory;
    }

    /// <summary>
    /// Verifies the container hands out the localized describer, which is what
    /// makes every <c>UserManager</c> in the application produce translated
    /// errors.
    /// </summary>
    [Fact]
    public void The_container_resolves_the_localized_describer()
    {
        using IServiceScope scope = factory.Services.CreateScope();

        IdentityErrorDescriber describer =
            scope.ServiceProvider.GetRequiredService<IdentityErrorDescriber>();

        Assert.IsType<LocalizedIdentityErrorDescriber>(describer);
    }

    /// <summary>
    /// Verifies that a password the policy refuses comes back with the
    /// description the Indonesian resource file holds for that error.
    /// </summary>
    /// <remarks>
    /// This calls the same <c>UserManager.CreateAsync</c> the registration page
    /// calls, so it exercises the real password validators and the real
    /// describer. The password has a lowercase letter, a digit and a symbol but
    /// no uppercase letter, which is exactly one policy failure and makes the
    /// expected error unambiguous.
    /// </remarks>
    [Fact]
    public async Task A_refused_password_is_described_in_indonesian()
    {
        string expectedDescription = ReadExpectedDescription(
            TestResourceFiles.IndonesianFileName);

        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo indonesianCulture = new(SupportedLanguages.Indonesian);
            CultureInfo.CurrentUICulture = indonesianCulture;
            CultureInfo.CurrentCulture = indonesianCulture;

            using IServiceScope scope = factory.Services.CreateScope();

            UserManager<ApplicationUser> userManager =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            string uniqueEmail = $"weak.password.{Guid.NewGuid():N}@example.test";

            ApplicationUser user = new()
            {
                UserName = uniqueEmail,
                Email = uniqueEmail,
                DisplayName = "Weak Password Probe",
                RequestedRole = ApplicationRoleNames.OtherDoctor,
                AccountStatus = AccountStatus.Pending,
            };

            IdentityResult result = await userManager.CreateAsync(user, "weakpassword1!");

            Assert.False(result.Succeeded);

            IdentityError uppercaseError = Assert.Single(
                result.Errors,
                error => string.Equals(error.Code, "PasswordRequiresUpper", StringComparison.Ordinal));

            Assert.Equal(expectedDescription, uppercaseError.Description);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    /// <summary>
    /// Verifies that the registration page renders the Indonesian refusal when
    /// the language cookie selects Indonesian.
    /// </summary>
    /// <remarks>
    /// The unit tests prove the describer reads the resx; this proves the whole
    /// chain, from the cookie through request localisation to the sentence the
    /// reader sees. It is the difference between "the string exists" and "the
    /// page shows it".
    /// </remarks>
    [Fact]
    public async Task The_registration_page_refuses_a_weak_password_in_indonesian()
    {
        string expectedDescription = ReadExpectedDescription(
            TestResourceFiles.IndonesianFileName);

        using HttpClient client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        await SelectIndonesianAsync(client);

        AntiforgeryFormHelper formHelper = new(client);

        string uniqueEmail = $"weak.page.{Guid.NewGuid():N}@example.test";

        using HttpResponseMessage registerResponse = await formHelper.PostFormAsync(
            "/Account/Register",
            "register",
            new Dictionary<string, string>
            {
                ["Input.DisplayName"] = "Weak Page Probe",
                ["Input.Email"] = uniqueEmail,
                ["Input.RequestedRole"] = ApplicationRoleNames.OtherDoctor,
                ["Input.Password"] = "weakpassword1!",
                ["Input.ConfirmPassword"] = "weakpassword1!",
            });

        string pageHtml = await registerResponse.Content.ReadAsStringAsync();

        // Blazor encodes the sentence's apostrophes as HTML entities, so the
        // page is compared in decoded form: the assertion is about what the
        // reader sees, not about how it was escaped on the wire.
        string renderedText = WebUtility.HtmlDecode(pageHtml);

        Assert.Contains(expectedDescription, renderedText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads the expected sentence for <see cref="ExpectedErrorKey"/> out of the
    /// named resource file, so the assertion tracks the translation rather than
    /// duplicating it.
    /// </summary>
    /// <param name="resourceFileName">The resource file to read.</param>
    /// <returns>The sentence the file holds for the expected error.</returns>
    private static string ReadExpectedDescription(string resourceFileName)
    {
        IReadOnlyDictionary<string, string> resources =
            TestResourceFiles.LoadValues(resourceFileName);

        bool found = resources.TryGetValue(ExpectedErrorKey, out string? description);

        Assert.True(found, $"{resourceFileName} must hold a value for {ExpectedErrorKey}.");

        string expectedDescription = description ?? string.Empty;

        Assert.NotEmpty(expectedDescription);

        return expectedDescription;
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
