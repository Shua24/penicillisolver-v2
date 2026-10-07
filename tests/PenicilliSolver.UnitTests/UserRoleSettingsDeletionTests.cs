using System.Security.Claims;

using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

using penicillisolver_v2.Components.Pages;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;

namespace PenicilliSolver.UnitTests;

public sealed class UserRoleSettingsDeletionTests : UserAdministrationTestFixture
{
    [Fact]
    public async Task Delete_requires_confirmation_and_cancel_preserves_the_account()
    {
        ApplicationUser administrator = await CreatePathologistAsync(
            "administrator@example.test", AccountStatus.Active);
        ApplicationUser doctor = await CreateUserAsync(
            "doctor@example.test", ApplicationRoleNames.OtherDoctor, AccountStatus.Active);
        using TestContext context = new();
        IRenderedComponent<UserRoleSettings> component = RenderSettings(context, administrator);

        IElement deleteButton = FindButton(component, doctor, "Delete");
        deleteButton.Click();

        ApplicationUser? beforeConfirmation = await UserManager.FindByIdAsync(doctor.Id);
        Assert.NotNull(beforeConfirmation);
        Assert.Contains("This cannot be undone.", component.Markup);
        IElement cancelButton = FindButton(component, doctor, "Cancel");
        cancelButton.Click();

        Assert.DoesNotContain("Confirm delete", component.Markup);
        ApplicationUser? afterCancel = await UserManager.FindByIdAsync(doctor.Id);
        Assert.NotNull(afterCancel);

        deleteButton = FindButton(component, doctor, "Delete");
        deleteButton.Click();
        IElement confirmButton = FindButton(component, doctor, "Confirm delete");
        confirmButton.Click();

        component.WaitForAssertion(() =>
        {
            IReadOnlyList<IElement> rows = component.FindAll("tbody tr");
            Assert.DoesNotContain(rows, row => row.TextContent.Contains(doctor.Email!, StringComparison.Ordinal));
        });
        ApplicationUser? afterConfirmation = await UserManager.FindByIdAsync(doctor.Id);
        Assert.Null(afterConfirmation);
        IElement remainingDeleteButton = FindButton(component, administrator, "Delete");
        bool isDisabled = remainingDeleteButton.HasAttribute("disabled");
        Assert.False(isDisabled);
    }

    [Fact]
    public async Task Refusal_explains_deletion_and_requires_a_fresh_confirmation()
    {
        ApplicationUser administrator = await CreatePathologistAsync(
            "administrator@example.test", AccountStatus.Active);
        using TestContext context = new();
        IRenderedComponent<UserRoleSettings> component = RenderSettings(context, administrator);

        IElement deleteButton = FindButton(component, administrator, "Delete");
        deleteButton.Click();
        IElement confirmButton = FindButton(component, administrator, "Confirm delete");
        confirmButton.Click();

        component.WaitForAssertion(() => Assert.Contains(
            "Deleting this account would leave nobody able to manage users or the spreadsheet.",
            component.Markup));
        Assert.DoesNotContain("Confirm delete", component.Markup);
        ApplicationUser? survivingAccount = await UserManager.FindByIdAsync(administrator.Id);
        Assert.NotNull(survivingAccount);
        deleteButton = FindButton(component, administrator, "Delete");
        bool isDisabled = deleteButton.HasAttribute("disabled");
        Assert.False(isDisabled);
        deleteButton.Click();
        confirmButton = FindButton(component, administrator, "Confirm delete");
        bool confirmationIsDisabled = confirmButton.HasAttribute("disabled");
        Assert.False(confirmationIsDisabled);
    }

    private IRenderedComponent<UserRoleSettings> RenderSettings(
        TestContext context, ApplicationUser administrator)
    {
        TestAuthorizationContext authorization = context.AddTestAuthorization();
        authorization.SetAuthorized(administrator.Email!);
        authorization.SetClaims(new Claim(ClaimTypes.NameIdentifier, administrator.Id));
        context.Services.AddSingleton(AdministrationService);

        IRenderedComponent<UserRoleSettings> component = context.RenderComponent<UserRoleSettings>();
        component.WaitForElement("tbody tr");
        return component;
    }

    private static IElement FindButton(
        IRenderedComponent<UserRoleSettings> component, ApplicationUser account, string label)
    {
        IElement row = component.FindAll("tbody tr")
            .Single(element => element.TextContent.Contains(account.Email!, StringComparison.Ordinal));
        IElement button = row.QuerySelectorAll("button")
            .Single(element => element.TextContent.Trim() == label);
        return button;
    }
}
