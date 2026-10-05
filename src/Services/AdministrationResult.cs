namespace penicillisolver_v2.Services;

using penicillisolver_v2.Domain.Enums;

/// <summary>
/// The outcome of one administration mutation. Either the write happened, or it
/// did not and a single user facing message explains why.
/// </summary>
public sealed class AdministrationResult
{
    private AdministrationResult(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    /// <summary>Whether the mutation was applied.</summary>
    public bool IsSuccess { get; }

    /// <summary>A confirmation sentence on success, or the refusal explanation on failure.</summary>
    public string Message { get; }

    /// <summary>Creates the success outcome.</summary>
    /// <param name="message">The confirmation shown to the user.</param>
    /// <returns>A successful result.</returns>
    public static AdministrationResult Success(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        AdministrationResult result = new(isSuccess: true, message: message);

        return result;
    }

    /// <summary>Creates the failure outcome.</summary>
    /// <param name="message">Why the mutation was refused, in user facing terms.</param>
    /// <returns>A failed result.</returns>
    public static AdministrationResult Failure(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        AdministrationResult result = new(isSuccess: false, message: message);

        return result;
    }
}

/// <summary>
/// The account state an administrator can assign. It mirrors
/// <see cref="AccountStatus"/> but deliberately excludes
/// <see cref="AccountStatus.Pending"/>, because returning an account to pending
/// is not an operation the settings page offers: pending is the state a
/// registration starts in and activation is one way.
/// </summary>
public enum AdministrableAccountState
{
    /// <summary>The account may sign in and use its role's permissions.</summary>
    Active = 1,

    /// <summary>The account is blocked and can no longer sign in.</summary>
    Disabled = 2,
}
