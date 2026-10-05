using Microsoft.AspNetCore.Authorization;

using penicillisolver_v2.Domain.Constants;

namespace penicillisolver_v2.Services;

/// <summary>
/// Registers the four application authorization policies. Every policy requires
/// both the appropriate role and an active account, so an account that has been
/// registered but not yet activated is denied everywhere.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Adds the role binding for the active account requirement and registers
    /// the four policies.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAuthorizationHandler, ActiveAccountHandler>();

        // Scoped, not singleton: this handler reads the team permission row from
        // the database, and a DbContext is scoped.
        services.AddScoped<IAuthorizationHandler, TeamSpreadsheetPermissionHandler>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicyNames.CanManageSpreadsheet,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(ApplicationRoleNames.ClinicalPathologist)
                    .AddRequirements(new ActiveAccountRequirement()));

            options.AddPolicy(
                AuthorizationPolicyNames.CanReadSpreadsheet,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(
                        ApplicationRoleNames.ClinicalPathologist,
                        ApplicationRoleNames.OtherDoctor,
                        ApplicationRoleNames.InfectiousDiseaseControlTeam)
                    .AddRequirements(new ActiveAccountRequirement()));

            options.AddPolicy(
                AuthorizationPolicyNames.CanManageUserRoles,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(ApplicationRoleNames.ClinicalPathologist)
                    .AddRequirements(new ActiveAccountRequirement()));

            // Managing the abbreviation mappings is a clinical pathologist task.
            // The other two roles may read the mappings but never write them, and
            // the service enforces the same policy server-side, so hiding the
            // controls on the page is a convenience and not the gate.
            options.AddPolicy(
                AuthorizationPolicyNames.CanManageAntibioticMappings,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(ApplicationRoleNames.ClinicalPathologist)
                    .AddRequirements(new ActiveAccountRequirement()));

            // Deliberately NOT restricted to the clinical pathologist role alone.
            // The infectious disease control team holds these permissions
            // whenever the team record grants them, and only the database knows
            // that. The handler decides; the policy only requires an active
            // account. Update and delete are SEPARATE policies because the
            // pathologist may grant one without the other.
            options.AddPolicy(
                AuthorizationPolicyNames.CanUpdateSpreadsheet,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(
                        ApplicationRoleNames.ClinicalPathologist,
                        ApplicationRoleNames.InfectiousDiseaseControlTeam)
                    .AddRequirements(new ActiveAccountRequirement())
                    .AddRequirements(new TeamSpreadsheetPermissionRequirement(
                        TeamSpreadsheetPermissionKind.Update)));

            options.AddPolicy(
                AuthorizationPolicyNames.CanDeleteSpreadsheet,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(
                        ApplicationRoleNames.ClinicalPathologist,
                        ApplicationRoleNames.InfectiousDiseaseControlTeam)
                    .AddRequirements(new ActiveAccountRequirement())
                    .AddRequirements(new TeamSpreadsheetPermissionRequirement(
                        TeamSpreadsheetPermissionKind.Delete)));
        });

        return services;
    }
}
