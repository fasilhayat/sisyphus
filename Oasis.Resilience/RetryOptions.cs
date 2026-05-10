namespace Oasis.Resilience;

using Microsoft.Extensions.Logging;

/// <summary>
/// Configuration options for retry behavior. Provides global defaults that are used when an attribute
/// omits values (i.e., the attribute parameter is left unspecified or set to the sentinel <c>-1</c>).
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Gets or sets the log level used by resilience actors when no <see cref="ILogger"/> is provided.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Debug;

    /// <summary>
    /// Gets or sets the default maximum number of retry attempts when not specified on the attribute.
    /// </summary>
    public int DefaultMaxAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the default initial delay (in milliseconds) before the first retry when not specified on the attribute.
    /// </summary>
    public int DefaultInitialDelayMs { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the optional list of exception types that should trigger a retry.
    /// When <c>null</c> (the default) every exception is retried. When set, only matching exceptions
    /// (or their subclasses) are retried; all others propagate immediately.
    /// </summary>
    public Type[]? DefaultRetryOnExceptions { get; set; }

    /// <summary>
    /// Gets or sets the upper bound (in milliseconds) for the exponential backoff between retry attempts.
    /// Once the computed delay exceeds this cap it is clamped, preventing extremely long waits and
    /// arithmetic overflow on high attempt counts.
    /// </summary>
    public int MaxDelayMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the jitter factor applied to each computed retry delay. The actual delay is
    /// multiplied by a uniform random number in <c>[1 - JitterFactor, 1 + JitterFactor]</c> to avoid
    /// synchronized retry storms across concurrent callers. Set to <c>0</c> to disable jitter.
    /// </summary>
    public double JitterFactor { get; set; } = 0.2;

    /// <summary>
    /// Gets or sets the timeout used by the proxy when asking actors to execute operations
    /// (retry, supervision and fan-out). Defaults to 30 seconds.
    /// </summary>
    public TimeSpan AskTimeout { get; set; } = TimeSpan.FromSeconds(30);
}