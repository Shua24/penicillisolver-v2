using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;

namespace penicillisolver_v2.Services;

/// <summary>
/// The out-of-band break-glass path: sets a clinical pathologist's password from
/// a shell.
/// </summary>
/// <remarks>
/// <para>
/// This exists for exactly one situation. Every recovery action inside the
/// application requires an active clinical pathologist, so once there is none
/// the application cannot recover itself by definition. The operator who has
/// shell access on the server is the only actor left, and this is the tool they
/// run.
/// </para>
/// <para>
/// It is deliberately narrow: pathologist accounts only, and it forces a
/// password change at the next sign-in. It is not a general "set anyone's
/// password" switch, because handing an operator that would make the shell
/// account a backdoor into every user in the system.
/// </para>
/// <para>
/// The password is read from standard input, never from argv: an argument is
/// visible to every user on the machine through <c>ps</c> and lands in the
/// shell's history file.
/// </para>
/// </remarks>
public static class PasswordRecoveryCommand
{
    /// <summary>The switch that selects this command instead of starting the host.</summary>
    public const string CommandLineSwitch = "--reset-pathologist-password";

    /// <summary>The switch naming the account to reset.</summary>
    public const string EmailOption = "--email";

    /// <summary>Exit code for a completed reset.</summary>
    private const int SuccessExitCode = 0;

    /// <summary>Exit code for a refusal or a failed write.</summary>
    private const int FailureExitCode = 1;

    /// <summary>Exit code for a malformed invocation.</summary>
    private const int UsageExitCode = 2;

    /// <summary>
    /// Reports whether the arguments ask for the recovery command.
    /// </summary>
    /// <param name="arguments">The process arguments.</param>
    /// <returns>True when the recovery switch is present.</returns>
    public static bool IsRequested(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        bool isRequested = arguments.Contains(CommandLineSwitch, StringComparer.Ordinal);

        return isRequested;
    }

    /// <summary>
    /// Runs the recovery command.
    /// </summary>
    /// <param name="arguments">The process arguments.</param>
    /// <returns>A process exit code.</returns>
    public static async Task<int> RunAsync(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string? email = ReadOption(arguments, EmailOption);

        if (string.IsNullOrWhiteSpace(email))
        {
            Console.Error.WriteLine($"{CommandLineSwitch} requires {EmailOption} <address>.");
            return UsageExitCode;
        }

        Console.Error.Write("New password: ");
        string? temporaryPassword = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(temporaryPassword))
        {
            Console.Error.WriteLine("No password was supplied.");
            return UsageExitCode;
        }

        // The command's own switches are stripped before the builder sees them:
        // "--reset-pathologist-password" is a bare flag, and the configuration
        // parser would otherwise pair it with the following argument as its
        // value.
        string[] hostArguments = StripOwnSwitches(arguments);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(hostArguments);

        // The command is a shell tool, so its output must be readable: the
        // application's own logging configuration would otherwise print every
        // SQL statement the reset issues. Only warnings and errors survive.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

        // A bare builder has no data-protection provider, and the reset token
        // provider needs one. The web host registers it implicitly; this path
        // must ask for it.
        builder.Services.AddDataProtection();

        builder.Services.AddDbContext<ApplicationDbContext>(
            DatabaseProviderSelector.Create(builder.Configuration, isDevelopment: false));

        builder.Services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        WebApplication application = builder.Build();

        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();

        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationDbContext database =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await database.Database.MigrateAsync();

        ApplicationUser? user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            Console.Error.WriteLine($"No account is registered for {email}.");
            return FailureExitCode;
        }

        bool holdsPathologistRole = await userManager.IsInRoleAsync(
            user,
            ApplicationRoleNames.ClinicalPathologist);

        if (!holdsPathologistRole)
        {
            Console.Error.WriteLine(
                $"{email} is not a clinical pathologist. This command only breaks a " +
                "pathologist lockout; in-application recovery covers every other role.");
            return FailureExitCode;
        }

        string resetToken = await userManager.GeneratePasswordResetTokenAsync(user);

        IdentityResult resetResult = await userManager.ResetPasswordAsync(
            user,
            resetToken,
            temporaryPassword);

        if (!resetResult.Succeeded)
        {
            Console.Error.WriteLine("Could not reset the password: " + DescribeErrors(resetResult));
            return FailureExitCode;
        }

        // The operator set a password the pathologist did not choose, so the
        // account is forced to replace it at the next sign-in. There is no
        // signed-in actor, so the "reset by" column stays null and the settings
        // page says the reset came from the server.
        user.MustChangePassword = true;
        user.LastPasswordResetByUserId = null;
        user.LastPasswordResetAtUtc = DateTimeOffset.UtcNow;

        IdentityResult updateResult = await userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            Console.Error.WriteLine(
                "The password was reset but the account could not be marked for a forced " +
                "change: " + DescribeErrors(updateResult));
            return FailureExitCode;
        }

        Console.Out.WriteLine(
            $"Password reset for {email}. The account must change it at next sign-in.");

        return SuccessExitCode;
    }

    /// <summary>
    /// Reads the value that follows an option, or null when the option is absent
    /// or has no value.
    /// </summary>
    /// <param name="arguments">The process arguments.</param>
    /// <param name="optionName">The option to look for.</param>
    /// <returns>The option's value, or null.</returns>
    private static string? ReadOption(string[] arguments, string optionName)
    {
        int optionIndex = Array.IndexOf(arguments, optionName);

        if (optionIndex < 0 || optionIndex + 1 >= arguments.Length)
        {
            return null;
        }

        string optionValue = arguments[optionIndex + 1];

        return optionValue;
    }

    /// <summary>
    /// Removes this command's own switches from the arguments before they reach
    /// the web host's configuration parser.
    /// </summary>
    /// <param name="arguments">The process arguments.</param>
    /// <returns>The arguments the host should see.</returns>
    private static string[] StripOwnSwitches(string[] arguments)
    {
        List<string> remainingArguments = new List<string>();

        for (int index = 0; index < arguments.Length; index++)
        {
            string argument = arguments[index];

            if (string.Equals(argument, CommandLineSwitch, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(argument, EmailOption, StringComparison.Ordinal))
            {
                // Skip the option's value as well as the option itself.
                index++;
                continue;
            }

            remainingArguments.Add(argument);
        }

        string[] strippedArguments = remainingArguments.ToArray();

        return strippedArguments;
    }

    /// <summary>
    /// Flattens an identity result's errors into one readable string.
    /// </summary>
    /// <param name="identityResult">The failed result.</param>
    /// <returns>A semicolon-separated list of descriptions.</returns>
    private static string DescribeErrors(IdentityResult identityResult)
    {
        IEnumerable<string> descriptions =
            identityResult.Errors.Select(error => error.Description);

        string joinedDescriptions = string.Join("; ", descriptions);

        return joinedDescriptions;
    }
}
