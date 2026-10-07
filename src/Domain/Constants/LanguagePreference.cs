namespace penicillisolver_v2.Domain.Constants;

/// <summary>
/// The cookie that remembers the language a user chose from the switcher. The
/// name is shared between the request localisation configuration and the
/// component that writes it, so the two can never drift apart.
/// </summary>
/// <remarks>
/// The value stored is the one the framework's own cookie provider expects: a
/// culture tag such as <c>id-ID</c> or <c>en-US</c>. Reading and writing it is
/// therefore done through the same provider rather than by inventing a format.
/// </remarks>
public static class LanguagePreference
{
    /// <summary>The name of the cookie carrying the chosen culture.</summary>
    public const string CookieName = ".AspNetCore.Culture";

    /// <summary>
    /// The form parameter the language switcher posts the chosen culture under.
    /// </summary>
    public const string FormFieldName = "culture";
}
