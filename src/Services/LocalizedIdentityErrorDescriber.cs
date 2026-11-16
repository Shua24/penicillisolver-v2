namespace penicillisolver_v2.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

using penicillisolver_v2.Resources;

/// <summary>
/// Replaces the framework's English-only identity error text with the
/// application's own resources.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IdentityErrorDescriber"/> is the framework's own extension point
/// for this. Every message an <see cref="IdentityResult"/> can carry is produced
/// by one of its virtual methods, so overriding them translates every call site
/// at once: the registration page, the change-password page, and the user
/// administration service all read <c>error.Description</c> and none of them has
/// to change. Without it, a weak password on the registration page renders
/// "Passwords must have at least one non alphanumeric character." in an
/// otherwise Indonesian interface.
/// </para>
/// <para>
/// The <see cref="IdentityError.Code"/> values are left exactly as the framework
/// sets them. Only the description is translated: the code is a stable
/// identifier a caller may switch on, and rewriting it would break that.
/// </para>
/// <para>
/// <c>AddErrorDescriber</c> registers this type scoped, so the injected
/// localizer resolves the culture of the request or circuit that is asking. The
/// localizer reads <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>
/// at lookup time rather than at construction, which is what lets a long lived
/// Blazor circuit follow the language switcher.
/// </para>
/// </remarks>
public sealed class LocalizedIdentityErrorDescriber : IdentityErrorDescriber
{
    private readonly IStringLocalizer localizer;

    /// <summary>Creates the describer over the application's shared resources.</summary>
    /// <param name="localizerFactory">The factory that resolves the shared resource set.</param>
    public LocalizedIdentityErrorDescriber(IStringLocalizerFactory localizerFactory)
    {
        ArgumentNullException.ThrowIfNull(localizerFactory);

        localizer = localizerFactory.Create(typeof(SharedResource));
    }

    /// <inheritdoc />
    public override IdentityError DefaultError() =>
        Build(nameof(DefaultError), "Identity_DefaultError");

    /// <inheritdoc />
    public override IdentityError ConcurrencyFailure() =>
        Build(nameof(ConcurrencyFailure), "Identity_ConcurrencyFailure");

    /// <inheritdoc />
    public override IdentityError PasswordMismatch() =>
        Build(nameof(PasswordMismatch), "Identity_PasswordMismatch");

    /// <inheritdoc />
    public override IdentityError InvalidToken() =>
        Build(nameof(InvalidToken), "Identity_InvalidToken");

    /// <inheritdoc />
    public override IdentityError LoginAlreadyAssociated() =>
        Build(nameof(LoginAlreadyAssociated), "Identity_LoginAlreadyAssociated");

    /// <inheritdoc />
    public override IdentityError InvalidUserName(string? userName) =>
        Build(nameof(InvalidUserName), "Identity_InvalidUserName", userName ?? string.Empty);

    /// <inheritdoc />
    public override IdentityError InvalidEmail(string? email) =>
        Build(nameof(InvalidEmail), "Identity_InvalidEmail", email ?? string.Empty);

    /// <inheritdoc />
    public override IdentityError DuplicateUserName(string userName) =>
        Build(nameof(DuplicateUserName), "Identity_DuplicateUserName", userName);

    /// <inheritdoc />
    public override IdentityError DuplicateEmail(string email) =>
        Build(nameof(DuplicateEmail), "Identity_DuplicateEmail", email);

    /// <inheritdoc />
    public override IdentityError InvalidRoleName(string? role) =>
        Build(nameof(InvalidRoleName), "Identity_InvalidRoleName", role ?? string.Empty);

    /// <inheritdoc />
    public override IdentityError DuplicateRoleName(string role) =>
        Build(nameof(DuplicateRoleName), "Identity_DuplicateRoleName", role);

    /// <inheritdoc />
    public override IdentityError UserAlreadyHasPassword() =>
        Build(nameof(UserAlreadyHasPassword), "Identity_UserAlreadyHasPassword");

    /// <inheritdoc />
    public override IdentityError UserLockoutNotEnabled() =>
        Build(nameof(UserLockoutNotEnabled), "Identity_UserLockoutNotEnabled");

    /// <inheritdoc />
    public override IdentityError UserAlreadyInRole(string role) =>
        Build(nameof(UserAlreadyInRole), "Identity_UserAlreadyInRole", role);

    /// <inheritdoc />
    public override IdentityError UserNotInRole(string role) =>
        Build(nameof(UserNotInRole), "Identity_UserNotInRole", role);

    /// <inheritdoc />
    public override IdentityError PasswordTooShort(int length) =>
        Build(nameof(PasswordTooShort), "Identity_PasswordTooShort", length);

    /// <inheritdoc />
    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Build(nameof(PasswordRequiresNonAlphanumeric), "Identity_PasswordRequiresNonAlphanumeric");

    /// <inheritdoc />
    public override IdentityError PasswordRequiresDigit() =>
        Build(nameof(PasswordRequiresDigit), "Identity_PasswordRequiresDigit");

    /// <inheritdoc />
    public override IdentityError PasswordRequiresLower() =>
        Build(nameof(PasswordRequiresLower), "Identity_PasswordRequiresLower");

    /// <inheritdoc />
    public override IdentityError PasswordRequiresUpper() =>
        Build(nameof(PasswordRequiresUpper), "Identity_PasswordRequiresUpper");

    /// <inheritdoc />
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Build(nameof(PasswordRequiresUniqueChars), "Identity_PasswordRequiresUniqueChars", uniqueChars);

    /// <inheritdoc />
    public override IdentityError RecoveryCodeRedemptionFailed() =>
        Build(nameof(RecoveryCodeRedemptionFailed), "Identity_RecoveryCodeRedemptionFailed");

    /// <summary>
    /// Builds one error, keeping the framework's code and translating only the
    /// description.
    /// </summary>
    /// <param name="code">The framework's stable error code.</param>
    /// <param name="resourceKey">The resource key holding the translated sentence.</param>
    /// <param name="arguments">Values for the sentence's placeholders, if any.</param>
    /// <returns>The error the framework will hand back to the caller.</returns>
    private IdentityError Build(string code, string resourceKey, params object[] arguments)
    {
        string description = localizer[resourceKey, arguments];

        IdentityError error = new()
        {
            Code = code,
            Description = description,
        };

        return error;
    }
}
