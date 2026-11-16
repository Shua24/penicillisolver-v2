using System.ComponentModel.DataAnnotations;
using System.Globalization;

using penicillisolver_v2.Components.Account.Validation;
using penicillisolver_v2.Domain.Constants;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Covers the DataAnnotations attributes that replace the framework's
/// English-only messages with the application's own resources.
/// </summary>
/// <remarks>
/// <para>
/// As with the identity describer, every assertion compares the produced message
/// against the value the resource file holds, formatted with the arguments the
/// attribute is expected to pass. A literal sentence here would be a second copy
/// of the translation and would break on the next wording change.
/// </para>
/// <para>
/// The field name is part of that comparison, not a separate concern: the
/// message's first placeholder is the <c>Field_</c> resource for the property
/// being validated, so a test that only checked the sentence would miss a
/// property whose field name never got translated. The field-name rules
/// themselves are covered in the sibling file.
/// </para>
/// </remarks>
public sealed partial class LocalizedValidationAttributeTests
{
    /// <summary>
    /// Verifies that a missing required value is reported with the Indonesian
    /// sentence and the translated field name.
    /// </summary>
    [Fact]
    public void Required_reads_its_message_from_the_indonesian_resx()
    {
        AssertMessageComesFrom(
            TestResourceFiles.IndonesianFileName,
            SupportedLanguages.Indonesian,
            "Validation_Required",
            memberName: "Password",
            displayName: "Password",
            validate: context => new LocalizedRequiredAttribute().GetValidationResult(null, context));
    }

    /// <summary>
    /// Verifies that an invalid email address is reported with the Indonesian
    /// sentence and the translated field name.
    /// </summary>
    [Fact]
    public void EmailAddress_reads_its_message_from_the_indonesian_resx()
    {
        AssertMessageComesFrom(
            TestResourceFiles.IndonesianFileName,
            SupportedLanguages.Indonesian,
            "Validation_EmailAddress",
            memberName: "Email",
            displayName: "Email",
            validate: context => new LocalizedEmailAddressAttribute()
                .GetValidationResult("not-an-address", context));
    }

    /// <summary>
    /// Verifies that a maximum-only length failure uses the maximum sentence and
    /// passes the maximum length through.
    /// </summary>
    [Fact]
    public void StringLength_maximum_reads_its_message_from_the_indonesian_resx()
    {
        AssertMessageComesFrom(
            TestResourceFiles.IndonesianFileName,
            SupportedLanguages.Indonesian,
            "Validation_MaximumLength",
            memberName: "DisplayName",
            displayName: "DisplayName",
            validate: context => new LocalizedStringLengthAttribute(200)
                .GetValidationResult(new string('a', 201), context),
            extraArguments: [200]);
    }

    /// <summary>
    /// Verifies that a length range failure uses the range sentence and passes
    /// the minimum and maximum through in that order.
    /// </summary>
    [Fact]
    public void StringLength_range_reads_its_message_from_the_indonesian_resx()
    {
        AssertMessageComesFrom(
            TestResourceFiles.IndonesianFileName,
            SupportedLanguages.Indonesian,
            "Validation_LengthRange",
            memberName: "Password",
            displayName: "Password",
            validate: context =>
            {
                LocalizedStringLengthAttribute attribute = new(100)
                {
                    MinimumLength = 6,
                };

                return attribute.GetValidationResult("abc", context);
            },
            extraArguments: [6, 100]);
    }

    /// <summary>
    /// Verifies that a confirmation mismatch is reported with the Indonesian
    /// sentence, naming both fields.
    /// </summary>
    [Fact]
    public void Compare_reads_its_message_from_the_indonesian_resx()
    {
        CompareModel model = new()
        {
            Password = "first",
            ConfirmPassword = "second",
        };

        AssertMessageComesFrom(
            TestResourceFiles.IndonesianFileName,
            SupportedLanguages.Indonesian,
            "Validation_Compare",
            memberName: "ConfirmPassword",
            displayName: "ConfirmPassword",
            validate: context => new LocalizedCompareAttribute("Password")
                .GetValidationResult("second", context),
            fieldMemberNames: ["Password", "ConfirmPassword"],
            model: model);
    }

    /// <summary>
    /// Verifies that the neutral resources are used when the UI culture is
    /// English, so the fallback is a real sentence rather than a key.
    /// </summary>
    [Fact]
    public void Required_reads_its_message_from_the_neutral_resx()
    {
        AssertMessageComesFrom(
            TestResourceFiles.NeutralFileName,
            SupportedLanguages.English,
            "Validation_Required",
            memberName: "Password",
            displayName: "Password",
            validate: context => new LocalizedRequiredAttribute().GetValidationResult(null, context));
    }

    /// <summary>
    /// Drives one attribute to failure and compares its message against the
    /// named resource file, formatted with the field name and any extra
    /// arguments.
    /// </summary>
    /// <param name="resourceFileName">The resource file the values must come from.</param>
    /// <param name="cultureTag">The culture to resolve the resources under.</param>
    /// <param name="messageKey">The resource key the attribute must use.</param>
    /// <param name="memberName">The property being validated.</param>
    /// <param name="displayName">The display name the context carries.</param>
    /// <param name="validate">Runs the attribute against the context.</param>
    /// <param name="extraArguments">Placeholders after the field names.</param>
    /// <param name="fieldMemberNames">
    /// The properties whose field names the sentence interpolates, in order. One
    /// entry for every message except the comparison, which names two.
    /// </param>
    /// <param name="model">The model instance, when the attribute needs one.</param>
    private static void AssertMessageComesFrom(
        string resourceFileName,
        string cultureTag,
        string messageKey,
        string memberName,
        string displayName,
        Func<ValidationContext, ValidationResult?> validate,
        object[]? extraArguments = null,
        string[]? fieldMemberNames = null,
        object? model = null)
    {
        IReadOnlyDictionary<string, string> resources =
            TestResourceFiles.LoadValues(resourceFileName);

        CultureInfo culture = new(cultureTag);

        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;

            ValidationContext context = CreateContext(
                model ?? new CompareModel(),
                memberName,
                displayName);

            ValidationResult? result = validate(context);

            Assert.NotNull(result);

            string[] fieldNames = fieldMemberNames ?? [memberName];

            object[] resolvedFieldNames = fieldNames
                .Select(name => resources.TryGetValue("Field_" + name, out string? value)
                    ? value
                    : displayName)
                .ToArray();

            object[] expectedArguments = [.. resolvedFieldNames, .. extraArguments ?? []];

            string expectedMessage = string.Format(
                culture,
                resources[messageKey],
                expectedArguments);

            Assert.Equal(expectedMessage, result.ErrorMessage);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    /// <summary>
    /// Builds a validation context carrying a real service provider, the way
    /// Blazor's <c>DataAnnotationsValidator</c> does.
    /// </summary>
    private static ValidationContext CreateContext(
        object model,
        string memberName,
        string displayName)
    {
        ValidationContext context = new(
            model,
            TestLocalizerFactory.CreateServiceProvider(),
            items: null)
        {
            MemberName = memberName,
            DisplayName = displayName,
        };

        return context;
    }

    /// <summary>
    /// A model with two properties, so <see cref="CompareAttribute"/> has
    /// something to compare against.
    /// </summary>
    private sealed class CompareModel
    {
        public string Password { get; set; } = string.Empty;

        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
