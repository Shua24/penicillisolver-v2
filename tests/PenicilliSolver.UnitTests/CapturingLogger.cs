using Microsoft.Extensions.Logging;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Records every log entry a service writes, so a test can assert that a
/// diagnostic actually happened rather than trusting the call by reading it.
/// </summary>
/// <remarks>
/// The upload service discards the reader's exception from the user facing
/// message and logs it instead. Without a test that inspects the log, removing
/// that call would silently lose the detail and every other test would still
/// pass.
/// </remarks>
/// <typeparam name="TCategoryName">The category the service logs under.</typeparam>
internal sealed class CapturingLogger<TCategoryName> : ILogger<TCategoryName>
{
    /// <summary>Every entry written so far, in order.</summary>
    public List<LogEntry> Entries { get; } = new();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        LogEntry entry = new(logLevel, exception, formatter(state, exception));

        Entries.Add(entry);
    }
}

/// <summary>One captured log entry.</summary>
/// <param name="Level">The severity it was written at.</param>
/// <param name="Exception">The exception attached to it, if any.</param>
/// <param name="Message">The formatted message.</param>
internal sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);
