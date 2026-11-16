using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using penicillisolver_v2.Resources;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Supplies a real <see cref="IStringLocalizerFactory"/> to unit tests that
/// construct a service by hand rather than resolving it from a container.
/// </summary>
/// <remarks>
/// A real factory (over the application's own resource files) is used rather
/// than a stub, because the services under test are only ever asserting on the
/// presence or absence of a message, never on its wording. Using the real
/// resources means a missing key would surface as a missing-key localizer
/// result here too, instead of being hidden behind a fake that always returns
/// what it is told to.
/// </remarks>
internal static class TestLocalizerFactory
{
    /// <summary>
    /// The shared localizer instance for tests that only need to satisfy a
    /// reader's parameter. Built once; it is stateless.
    /// </summary>
    public static IStringLocalizer Localizer { get; } =
        Create().Create(typeof(SharedResource));

    /// <summary>
    /// The closed localizer type a component injects. Registered into a bUnit
    /// container so a component's <c>@inject IStringLocalizer&lt;SharedResource&gt;</c>
    /// resolves; the open generic alone does not satisfy the closed type when it
    /// is registered as an instance.
    /// </summary>
    public static IStringLocalizer<SharedResource> ClosedLocalizer { get; } =
        new SharedResourceLocalizer(Create());

    /// <summary>Builds a localizer factory backed by the application resources.</summary>
    public static IStringLocalizerFactory Create()
    {
        IServiceProvider provider = CreateServiceProvider();

        IStringLocalizerFactory factory =
            provider.GetRequiredService<IStringLocalizerFactory>();

        return factory;
    }

    /// <summary>
    /// Builds a service provider that can supply an
    /// <see cref="IStringLocalizerFactory"/>, for tests that hand a
    /// <see cref="System.ComponentModel.DataAnnotations.ValidationContext"/> a
    /// provider the way Blazor's validator does.
    /// </summary>
    public static IServiceProvider CreateServiceProvider()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddLocalization();

        ServiceProvider provider = services.BuildServiceProvider();

        return provider;
    }

    /// <summary>
    /// Adapts an <see cref="IStringLocalizerFactory"/> to the closed
    /// <see cref="IStringLocalizer{T}"/> the components inject.
    /// </summary>
    private sealed class SharedResourceLocalizer : IStringLocalizer<SharedResource>
    {
        private readonly IStringLocalizer innerLocalizer;

        /// <summary>Creates a localizer for the application's shared resources using the supplied factory.</summary>
        public SharedResourceLocalizer(IStringLocalizerFactory factory)
        {
            innerLocalizer = factory.Create(typeof(SharedResource));
        }

        public LocalizedString this[string name] => innerLocalizer[name];

        public LocalizedString this[string name, params object[] arguments] =>
            innerLocalizer[name, arguments];

        /// <summary>Enumerates shared resource strings, optionally including parent culture resources.</summary>
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            innerLocalizer.GetAllStrings(includeParentCultures);
    }
}
