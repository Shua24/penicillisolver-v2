using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

using penicillisolver_v2.Components.Account.Validation;
using penicillisolver_v2.Domain.Constants;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Covers how a validation message names the field it is about, and guards the
/// convention that keeps those names translated.
/// </summary>
/// <remarks>
/// The message tests live in the sibling file. This half exists because the
/// field name is the part that fails quietly: a property with no <c>Field_</c>
/// resource still renders a complete sentence, just with an English identifier
/// inside it, and nothing in the build or the rest of the suite notices.
/// </remarks>
public sealed partial class LocalizedValidationAttributeTests
{
    /// <summary>
    /// Verifies that a property with no <c>Field_</c> resource falls back to its
    /// own display name instead of rendering a blank or a key.
    /// </summary>
    [Fact]
    public void A_property_without_a_field_resource_uses_its_display_name()
    {
        IReadOnlyDictionary<string, string> resources =
            TestResourceFiles.LoadValues(TestResourceFiles.IndonesianFileName);

        Assert.False(
            resources.ContainsKey("Field_RememberMe"),
            "This test needs a property that deliberately has no Field_ resource.");

        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(SupportedLanguages.Indonesian);

            ValidationContext context = CreateContext(
                model: new CompareModel(),
                memberName: "RememberMe",
                displayName: "Remember me");

            ValidationResult? result = new LocalizedRequiredAttribute()
                .GetValidationResult(null, context);

            Assert.NotNull(result);

            string expectedMessage = string.Format(
                new CultureInfo(SupportedLanguages.Indonesian),
                resources["Validation_Required"],
                "Remember me");

            Assert.Equal(expectedMessage, result.ErrorMessage);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    /// <summary>
    /// Verifies that a failed result names the property it belongs to, which is
    /// what routes the message beside the field on a whole-model validation.
    /// </summary>
    [Fact]
    public void A_failed_result_names_the_property_it_belongs_to()
    {
        ValidationContext context = CreateContext(
            model: new CompareModel(),
            memberName: "Password",
            displayName: "Password");

        ValidationResult? result = new LocalizedRequiredAttribute()
            .GetValidationResult(null, context);

        Assert.NotNull(result);
        Assert.Contains("Password", result.MemberNames, StringComparer.Ordinal);
    }

    /// <summary>
    /// Verifies that every property on an account page's input model that
    /// carries a localized validation attribute has a matching <c>Field_</c>
    /// resource in both languages.
    /// </summary>
    /// <remarks>
    /// Without this, adding a validated property and forgetting its field name
    /// does not break anything: the message falls back to the property name and
    /// renders "Password wajib diisi." with an English identifier in it. That is
    /// invisible in code review and invisible in a passing suite, so it is
    /// checked here instead.
    /// </remarks>
    [Fact]
    public void Every_validated_property_on_an_account_page_has_a_field_resource()
    {
        IReadOnlyDictionary<string, string> neutralResources =
            TestResourceFiles.LoadValues(TestResourceFiles.NeutralFileName);

        IReadOnlyDictionary<string, string> indonesianResources =
            TestResourceFiles.LoadValues(TestResourceFiles.IndonesianFileName);

        Type[] inputModelTypes = typeof(Program).Assembly.GetTypes()
            .Where(type => type.IsNested
                && string.Equals(type.Name, "InputModel", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(inputModelTypes);

        List<string> missingKeys = new();

        foreach (Type inputModelType in inputModelTypes)
        {
            foreach (PropertyInfo property in inputModelType.GetProperties())
            {
                object[] attributes = property.GetCustomAttributes(inherit: false);

                bool carriesLocalizedAttribute = attributes.Any(attribute =>
                    attribute is LocalizedRequiredAttribute
                        or LocalizedEmailAddressAttribute
                        or LocalizedStringLengthAttribute
                        or LocalizedCompareAttribute);

                if (!carriesLocalizedAttribute)
                {
                    continue;
                }

                string fieldKey = "Field_" + property.Name;

                bool isTranslated = neutralResources.ContainsKey(fieldKey)
                    && indonesianResources.ContainsKey(fieldKey);

                if (!isTranslated)
                {
                    missingKeys.Add(
                        $"{inputModelType.DeclaringType?.Name}.{property.Name} has no {fieldKey}");
                }
            }
        }

        Assert.True(
            missingKeys.Count == 0,
            "Validated properties with no field display name: " + string.Join(", ", missingKeys));
    }
}
