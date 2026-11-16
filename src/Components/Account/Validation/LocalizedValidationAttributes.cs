namespace penicillisolver_v2.Components.Account.Validation;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// The DataAnnotations attributes the account pages use, with their messages
/// resolved from the application's shared resources.
/// </summary>
/// <remarks>
/// <para>
/// Each type derives from the framework attribute it replaces and keeps that
/// attribute's validation logic untouched. Only the message is swapped, so the
/// rules a reader is held to are exactly the framework's rules; what changes is
/// that the sentence explaining a failure is in the reader's language.
/// </para>
/// <para>
/// They are drop-in replacements: <c>[Required]</c> becomes
/// <c>[LocalizedRequired]</c> and so on. The field name in the sentence comes
/// from a <c>Field_</c> resource keyed on the property name, so a message reads
/// "Kata sandi wajib diisi." rather than "Password wajib diisi.".
/// </para>
/// </remarks>
public sealed class LocalizedRequiredAttribute : RequiredAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        ValidationResult? baseResult = base.IsValid(value, validationContext);

        if (baseResult == ValidationResult.Success)
        {
            return ValidationResult.Success;
        }

        string fieldName = LocalizedValidationMessage.FieldName(validationContext);

        ValidationResult result = LocalizedValidationMessage.Build(
            validationContext,
            "Validation_Required",
            fieldName);

        return result;
    }
}

/// <summary>
/// Replaces <see cref="EmailAddressAttribute"/> with a localized message.
/// </summary>
/// <remarks>
/// Unusually, this one delegates rather than derives: <see cref="EmailAddressAttribute"/>
/// is sealed, so there is no base class to override. A single instance of the
/// framework attribute does the actual check, which keeps its matching rule
/// rather than a reimplementation that could drift from it.
/// </remarks>
public sealed class LocalizedEmailAddressAttribute : ValidationAttribute
{
    private static readonly EmailAddressAttribute EmailAddressValidator = new();

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        bool isValid = EmailAddressValidator.IsValid(value);

        if (isValid)
        {
            return ValidationResult.Success;
        }

        string fieldName = LocalizedValidationMessage.FieldName(validationContext);

        ValidationResult result = LocalizedValidationMessage.Build(
            validationContext,
            "Validation_EmailAddress",
            fieldName);

        return result;
    }
}

/// <summary>
/// Replaces <see cref="StringLengthAttribute"/> with a localized message, using
/// the maximum-only sentence when no minimum is set and the range sentence when
/// one is.
/// </summary>
public sealed class LocalizedStringLengthAttribute : StringLengthAttribute
{
    /// <summary>Creates the attribute with the supplied maximum length.</summary>
    /// <param name="maximumLength">The longest accepted value, in characters.</param>
    public LocalizedStringLengthAttribute(int maximumLength)
        : base(maximumLength)
    {
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        ValidationResult? baseResult = base.IsValid(value, validationContext);

        if (baseResult == ValidationResult.Success)
        {
            return ValidationResult.Success;
        }

        string fieldName = LocalizedValidationMessage.FieldName(validationContext);

        ValidationResult result = MinimumLength == 0
            ? LocalizedValidationMessage.Build(
                validationContext,
                "Validation_MaximumLength",
                fieldName,
                MaximumLength)
            : LocalizedValidationMessage.Build(
                validationContext,
                "Validation_LengthRange",
                fieldName,
                MinimumLength,
                MaximumLength);

        return result;
    }
}

/// <summary>
/// Replaces <see cref="CompareAttribute"/> with a localized message naming both
/// fields.
/// </summary>
/// <remarks>
/// The sentence names both fields so a reader can tell which two disagree. That
/// is only readable because the resource templates start with a word and the
/// <c>Field_</c> values are lowercase: a template beginning with a placeholder
/// would capitalise whichever field name landed mid-sentence.
/// </remarks>
public sealed class LocalizedCompareAttribute : CompareAttribute
{
    /// <summary>Creates the attribute comparing against the named property.</summary>
    /// <param name="otherProperty">The property this one must equal.</param>
    public LocalizedCompareAttribute(string otherProperty)
        : base(otherProperty)
    {
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        ValidationResult? baseResult = base.IsValid(value, validationContext);

        if (baseResult == ValidationResult.Success)
        {
            return ValidationResult.Success;
        }

        // The other field is named first, so the sentence reads "the password and
        // confirm password fields do not match" rather than the reverse.
        string otherFieldName = LocalizedValidationMessage.FieldName(
            validationContext,
            OtherProperty,
            OtherPropertyDisplayName ?? OtherProperty);

        string confirmFieldName = LocalizedValidationMessage.FieldName(validationContext);

        ValidationResult result = LocalizedValidationMessage.Build(
            validationContext,
            "Validation_Compare",
            otherFieldName,
            confirmFieldName);

        return result;
    }
}
