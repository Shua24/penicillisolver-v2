using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;

using penicillisolver_v2.Domain.Entities;

namespace penicillisolver_v2.Services;

/// <summary>
/// A stand-in email sender that writes nothing to the network. Account
/// confirmation is not required by this application, so the links it would
/// carry are never used; replace this with a real sender before shipping a
/// workflow that depends on email delivery.
/// </summary>
public sealed class IdentityNoOpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly IEmailSender emailSender = new NoOpEmailSender();

    /// <inheritdoc />
    public Task SendConfirmationLinkAsync(
        ApplicationUser user,
        string email,
        string confirmationLink) =>
        emailSender.SendEmailAsync(
            email,
            "Confirm your email",
            $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

    /// <inheritdoc />
    public Task SendPasswordResetLinkAsync(
        ApplicationUser user,
        string email,
        string resetLink) =>
        emailSender.SendEmailAsync(
            email,
            "Reset your password",
            $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

    /// <inheritdoc />
    public Task SendPasswordResetCodeAsync(
        ApplicationUser user,
        string email,
        string resetCode) =>
        emailSender.SendEmailAsync(
            email,
            "Reset your password",
            $"Please reset your password using the following code: {resetCode}");
}
