namespace penicillisolver_v2.Resources;

/// <summary>
/// Marker type for <c>IStringLocalizer&lt;SharedResource&gt;</c>. It carries no
/// members of its own: the resource files beside it
/// (<c>SharedResource.resx</c> for the neutral English fallback and
/// <c>SharedResource.id.resx</c> for Indonesian) hold every user facing string.
/// </summary>
/// <remarks>
/// A component or service that needs a translated string injects
/// <c>IStringLocalizer&lt;SharedResource&gt;</c> and reads it with the indexer,
/// <c>localizer["Key"]</c>. That form returns a <see cref="LocalizedString"/>
/// which already formats any supplied arguments, and which falls back to the
/// neutral resource and then to the key itself rather than throwing when a key
/// is missing.
/// </remarks>
public sealed class SharedResource
{
}
