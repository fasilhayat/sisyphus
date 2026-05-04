namespace Oasis.Resilience.Attributes;

/// <summary>
/// Defines the supervision strategies available for actor failure handling.
/// </summary>
public enum SupervisionStrategy
{
    /// <summary>
    /// Restart the actor when it fails.
    /// </summary>
    Restart,

    /// <summary>
    /// Stop the actor when it fails.
    /// </summary>
    Stop,

    /// <summary>
    /// Escalate the failure to the parent actor.
    /// </summary>
    Escalate,

    /// <summary>
    /// Resume the actor without restarting when it fails.
    /// </summary>
    Resume,

    /// <summary>
    /// Restart the actor with exponential backoff when it fails.
    /// </summary>
    RestartWithBackoff
}
