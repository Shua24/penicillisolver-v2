using System.Globalization;
using System.Reflection;

using Microsoft.AspNetCore.Identity;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Services;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Covers the describer that replaces the framework's English-only identity
/// error text.
/// </summary>
/// <remarks>
/// <para>
/// Every assertion here compares the describer's output against the value the
/// resource file actually holds for the error's key. Asserting a translated
/// sentence directly would make the test a second copy of the translation: it
/// would fail whenever the wording is improved, and it would pass even if the
/// describer had returned its own hardcoded string without touching a resx.
/// Comparing against the file proves the sentence came from the resx, and it
/// survives a translation rewrite untouched.
/// </para>
/// <para>
/// The one structural test, <see cref="Every_error_method_is_overridden"/>, is
/// deliberate: it is the check that catches a framework upgrade adding an error
/// method this type does not translate, which no wording comparison can detect.
/// </para>
/// </remarks>
public sealed class LocalizedIdentityErrorDescriberTests
{
    /// <summary>
    /// Verifies that every error method the framework declares is overridden
    /// here, so no code can fall back to the English base implementation.
    /// </summary>
    [Fact]
    public void Every_error_method_is_overridden()
    {
        List<string> missingOverrides = new();

        foreach (MethodInfo frameworkMethod in DeclaredErrorMethods())
        {
            MethodInfo? overrideMethod = FindOverride(frameworkMethod);

            if (overrideMethod is null)
            {
                missingOverrides.Add(frameworkMethod.Name);
            }
        }

        Assert.True(
            missingOverrides.Count == 0,
            "Identity error methods with no localized override: "
                + string.Join(", ", missingOverrides));
    }

    /// <summary>
    /// Verifies that every error description matches the Indonesian resource
    /// file's value for that error's key.
    /// </summary>
    [Fact]
    public void Every_description_comes_from_the_indonesian_resx()
    {
        AssertDescriptionsComeFrom(
            TestResourceFiles.IndonesianFileName,
            SupportedLanguages.Indonesian);
    }

    /// <summary>
    /// Verifies that every error description matches the neutral resource file's
    /// value for that error's key when the UI culture is English.
    /// </summary>
    [Fact]
    public void Every_description_comes_from_the_neutral_resx()
    {
        AssertDescriptionsComeFrom(
            TestResourceFiles.NeutralFileName,
            SupportedLanguages.English);
    }

    /// <summary>
    /// Verifies that each error keeps the framework's own code, so a caller that
    /// switches on the code is unaffected by translation.
    /// </summary>
    [Fact]
    public void Every_error_keeps_the_framework_code()
    {
        LocalizedIdentityErrorDescriber describer = CreateDescriber();

        List<string> mismatches = new();

        foreach (MethodInfo frameworkMethod in DeclaredErrorMethods())
        {
            object[] arguments = RepresentativeArguments(frameworkMethod);

            IdentityError error = Invoke(describer, frameworkMethod, arguments);

            if (!string.Equals(error.Code, frameworkMethod.Name, StringComparison.Ordinal))
            {
                mismatches.Add($"{frameworkMethod.Name}: got code '{error.Code}'");
            }
        }

        Assert.True(
            mismatches.Count == 0,
            "Identity error codes that were rewritten: " + string.Join(", ", mismatches));
    }

    /// <summary>
    /// Compares every error description against the named resource file, under
    /// the supplied UI culture.
    /// </summary>
    /// <param name="resourceFileName">The resource file the values must come from.</param>
    /// <param name="cultureTag">The culture to resolve the resources under.</param>
    private static void AssertDescriptionsComeFrom(string resourceFileName, string cultureTag)
    {
        IReadOnlyDictionary<string, string> resources =
            TestResourceFiles.LoadValues(resourceFileName);

        CultureInfo culture = new(cultureTag);

        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;

        try
        {
            // Both are set because the localizer resolves the resource by the UI
            // culture and formats its placeholders by the formatting culture.
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;

            LocalizedIdentityErrorDescriber describer = CreateDescriber();

            List<string> mismatches = new();

            foreach (MethodInfo frameworkMethod in DeclaredErrorMethods())
            {
                object[] arguments = RepresentativeArguments(frameworkMethod);

                string expectedKey = "Identity_" + frameworkMethod.Name;

                if (!resources.TryGetValue(expectedKey, out string? template))
                {
                    mismatches.Add($"{expectedKey} is missing from {resourceFileName}");
                    continue;
                }

                string expectedDescription = string.Format(culture, template, arguments);

                IdentityError error = Invoke(describer, frameworkMethod, arguments);

                if (!string.Equals(error.Description, expectedDescription, StringComparison.Ordinal))
                {
                    mismatches.Add(
                        $"{expectedKey}: {resourceFileName} holds \"{expectedDescription}\" "
                        + $"but the describer returned \"{error.Description}\"");
                }
            }

            Assert.True(
                mismatches.Count == 0,
                $"Descriptions that did not come from {resourceFileName}:"
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, mismatches));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    /// <summary>Builds the describer over the application's real resources.</summary>
    private static LocalizedIdentityErrorDescriber CreateDescriber()
    {
        LocalizedIdentityErrorDescriber describer = new(TestLocalizerFactory.Create());

        return describer;
    }

    /// <summary>
    /// Lists the error methods the framework declares on its own type, which is
    /// the set this describer has to cover.
    /// </summary>
    private static MethodInfo[] DeclaredErrorMethods()
    {
        MethodInfo[] methods = typeof(IdentityErrorDescriber)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.ReturnType == typeof(IdentityError))
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(methods);

        return methods;
    }

    /// <summary>
    /// Finds this describer's override of one framework method, or null when it
    /// does not override it.
    /// </summary>
    private static MethodInfo? FindOverride(MethodInfo frameworkMethod)
    {
        MethodInfo? overrideMethod = typeof(LocalizedIdentityErrorDescriber).GetMethod(
            frameworkMethod.Name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
            binder: null,
            types: frameworkMethod.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray(),
            modifiers: null);

        return overrideMethod;
    }

    /// <summary>
    /// Supplies one value per parameter: a plausible length for the numeric
    /// parameters and a placeholder name for the string ones.
    /// </summary>
    private static object[] RepresentativeArguments(MethodInfo frameworkMethod)
    {
        object[] arguments = frameworkMethod.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(int)
                ? (object)6
                : "probe")
            .ToArray();

        return arguments;
    }

    /// <summary>Invokes one error method on the describer.</summary>
    private static IdentityError Invoke(
        LocalizedIdentityErrorDescriber describer,
        MethodInfo frameworkMethod,
        object[] arguments)
    {
        IdentityError? error = frameworkMethod.Invoke(describer, arguments) as IdentityError;

        Assert.NotNull(error);

        return error;
    }
}
