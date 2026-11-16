namespace penicillisolver_v2.Components.Account.Validation;

using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Localization;

using penicillisolver_v2.Resources;

/// <summary>
/// Resolves the text of a DataAnnotations validation message from the
/// application's shared resources.
/// </summary>
/// <remarks>
/// <para>
/// The built-in attributes render English because their messages come from the
/// framework. There is no supported way to point them at an
/// <see cref="IStringLocalizer"/>: <c>ErrorMessageResourceType</c> needs a type
/// exposing a static <c>ResourceManager</c>, and <see cref="SharedResource"/> is
/// a bare marker class with no members of its own. So the localized variants
/// derive from the built-in attribute, keep its validation logic, and replace
/// only the message.
/// </para>
/// <para>
/// The localizer is reached through <see cref="ValidationContext.GetService"/>.
/// That works because Blazor's <c>DataAnnotationsValidator</c> passes its own
/// service provider into every <see cref="ValidationContext"/> it builds, so the
/// factory is the request's or circuit's, and the culture is the one the reader
/// chose. A validation run that reaches here without a service provider is a
/// wiring mistake — <c>AddLocalization()</c> missing, or a caller using
/// <c>Validator.TryValidateObject</c> instead of the component — and it fails
/// loudly rather than silently falling back to English.
/// </para>
/// <para>
/// Field names are looked up by convention: the <c>MemberName</c> of the
/// property being validated plus a <c>Field_</c> prefix, so <c>Email</c> reads
/// as <c>Field_Email</c>. When no such key exists the property's own display
/// name is used, which keeps a newly added property readable instead of blank.
/// </para>
/// </remarks>
internal static class LocalizedValidationMessage
{
    /// <summary>The prefix that marks a resource key as a field display name.</summary>
    private const string FieldKeyPrefix = "Field_";

    /// <summary>
    /// Builds a localized validation result, attached to the property being
    /// validated.
    /// </summary>
    /// <param name="validationContext">The context the attribute was called with.</param>
    /// <param name="resourceKey">The resource key holding the sentence.</param>
    /// <param name="arguments">Values for the sentence's placeholders.</param>
    /// <returns>A failed result with the localized message.</returns>
    public static ValidationResult Build(
        ValidationContext validationContext,
        string resourceKey,
        params object[] arguments)
    {
        IStringLocalizer localizer = ResolveLocalizer(validationContext);

        string message = localizer[resourceKey, arguments];

        // The result must name the property it belongs to. Blazor's whole-model
        // pass on submit routes a message by MemberNames, so a result without
        // them would show in the validation summary only, never beside the
        // field. This mirrors the framework's own failure result, which is
        // private protected and therefore not reachable from here.
        string[]? memberNames = validationContext.MemberName is { } memberName
            ? [memberName]
            : null;

        ValidationResult result = new(message, memberNames);

        return result;
    }

    /// <summary>
    /// Returns the localized display name of the property being validated.
    /// </summary>
    /// <param name="validationContext">The context the attribute was called with.</param>
    /// <returns>The reader facing field name.</returns>
    public static string FieldName(ValidationContext validationContext)
    {
        string memberName = validationContext.MemberName ?? string.Empty;

        string fieldName = ResolveFieldName(
            validationContext,
            memberName,
            validationContext.DisplayName);

        return fieldName;
    }

    /// <summary>
    /// Returns the localized display name of a named property, falling back to
    /// the supplied name when no field resource exists for it.
    /// </summary>
    /// <param name="validationContext">The context the attribute was called with.</param>
    /// <param name="memberName">The property to name.</param>
    /// <param name="fallbackName">The name to use when no field resource exists.</param>
    /// <returns>The reader facing field name.</returns>
    public static string FieldName(
        ValidationContext validationContext,
        string memberName,
        string fallbackName)
    {
        string fieldName = ResolveFieldName(validationContext, memberName, fallbackName);

        return fieldName;
    }

    /// <summary>
    /// Resolves the field name for one property, preferring the resource value
    /// and falling back to the supplied name.
    /// </summary>
    private static string ResolveFieldName(
        ValidationContext validationContext,
        string memberName,
        string fallbackName)
    {
        if (memberName.Length == 0)
        {
            return fallbackName;
        }

        IStringLocalizer localizer = ResolveLocalizer(validationContext);

        string fieldKey = FieldKeyPrefix + memberName;

        LocalizedString localizedFieldName = localizer[fieldKey];

        if (localizedFieldName.ResourceNotFound)
        {
            return fallbackName;
        }

        return localizedFieldName.Value;
    }

    /// <summary>
    /// Resolves the shared-resource localizer from the validation context's
    /// service provider.
    /// </summary>
    /// <param name="validationContext">The context the attribute was called with.</param>
    /// <returns>The localizer over the application's shared resources.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no localizer factory is reachable, which means the application
    /// did not register localization or validated outside a Blazor edit context.
    /// </exception>
    private static IStringLocalizer ResolveLocalizer(ValidationContext validationContext)
    {
        IStringLocalizerFactory? localizerFactory =
            validationContext.GetService(typeof(IStringLocalizerFactory)) as IStringLocalizerFactory;

        if (localizerFactory is null)
        {
            throw new InvalidOperationException(
                "A localized validation message needs an IStringLocalizerFactory. Register it with " +
                "AddLocalization() and validate through an EditForm's DataAnnotationsValidator, " +
                "which supplies the service provider. Validating through Validator.TryValidateObject " +
                "does not, so it cannot produce a translated message.");
        }

        IStringLocalizer localizer = localizerFactory.Create(typeof(SharedResource));

        return localizer;
    }
}
