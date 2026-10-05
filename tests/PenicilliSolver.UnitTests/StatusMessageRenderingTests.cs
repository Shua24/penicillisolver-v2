using Bunit;

using Microsoft.AspNetCore.Http;

using penicillisolver_v2.Components.Account.Shared;

using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Renders the account status message component the way a Blazor circuit does.
/// </summary>
/// <remarks>
/// An interactive (<c>@rendermode InteractiveServer</c>) component has no HTTP
/// request behind it, so the framework cascades NO <c>HttpContext</c>. These
/// tests deliberately render without one.
/// <para>
/// Why this file exists rather than an HTTP test: a request through
/// <c>HttpClient</c> only exercises the static prerender, which DOES have a real
/// HttpContext. The interactive render happens later over the circuit's
/// WebSocket and is never reached by a plain GET. A page test therefore passes
/// against a component that throws on every interactive render — which is what
/// happened to the upload page. Rendering the component directly, with no
/// cascading context, reproduces the real failure.
/// </para>
/// </remarks>
public sealed class StatusMessageRenderingTests : TestContext
{
    [Fact]
    public void Renders_without_an_http_context_when_a_message_is_supplied()
    {
        // This is the reported failure: the upload page renders this component
        // inside a circuit, passing Message directly, with no HttpContext.
        IRenderedComponent<StatusMessage> component = RenderComponent<StatusMessage>(
            parameters => parameters.Add(parameter => parameter.Message, "Error: upload rejected."));

        string renderedMarkup = component.Markup;

        Assert.Contains("upload rejected", renderedMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public void Renders_without_an_http_context_and_without_a_message()
    {
        // No message and no cookie source: the component must render empty
        // rather than throw.
        IRenderedComponent<StatusMessage> component = RenderComponent<StatusMessage>();

        string renderedMarkup = component.Markup;

        Assert.DoesNotContain("alert", renderedMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public void Applies_the_danger_class_to_an_error_message()
    {
        IRenderedComponent<StatusMessage> component = RenderComponent<StatusMessage>(
            parameters => parameters.Add(parameter => parameter.Message, "Error: bad file."));

        Assert.Contains("alert-danger", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Applies_the_success_class_to_a_non_error_message()
    {
        IRenderedComponent<StatusMessage> component = RenderComponent<StatusMessage>(
            parameters => parameters.Add(parameter => parameter.Message, "Upload accepted."));

        Assert.Contains("alert-success", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_the_status_cookie_when_an_http_context_is_cascaded()
    {
        // The static pages (login, register) DO have a context, so the cookie
        // fallback must keep working there.
        DefaultHttpContext httpContext = new();

        string cookieName = penicillisolver_v2.Services.IdentityRedirectManager.StatusCookieName;

        httpContext.Request.Headers.Cookie = cookieName + "=From-the-cookie";

        IRenderedComponent<StatusMessage> component = RenderComponent<StatusMessage>(
            parameters => parameters.AddCascadingValue<HttpContext>(httpContext));

        Assert.Contains("From-the-cookie", component.Markup, StringComparison.Ordinal);
    }
}
