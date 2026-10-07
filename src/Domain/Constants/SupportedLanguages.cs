namespace penicillisolver_v2.Domain.Constants;

/// <summary>
/// The languages the application offers and which one it uses when the user
/// has expressed no preference. Indonesian is the default: an unconfigured
/// browser reaches an Indonesian interface without doing anything.
/// </summary>
public static class SupportedLanguages
{
    /// <summary>The Indonesian culture tag. This is the application default.</summary>
    public const string Indonesian = "id-ID";

    /// <summary>The English culture tag, offered as the alternative.</summary>
    public const string English = "en-US";

    /// <summary>The two letter code for Indonesian, used in two letter comparisons.</summary>
    public const string IndonesianTwoLetter = "id";

    /// <summary>The two letter code for English, used in two letter comparisons.</summary>
    public const string EnglishTwoLetter = "en";

    /// <summary>The culture applied when no stored preference exists.</summary>
    public const string Default = Indonesian;

    /// <summary>Every culture the application accepts, in the order they are offered.</summary>
    public static readonly string[] All = [Indonesian, English];
}
