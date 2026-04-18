namespace Oasis.Resilience.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ResilientAttribute : Attribute
{
    public int MaxAttempts { get; }

    public int InitialDelaySeconds { get; }

    public ResilientAttribute(int maxAttempts = 5, int initialDelaySeconds = 2)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        if (initialDelaySeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(initialDelaySeconds));

        MaxAttempts = maxAttempts;
        InitialDelaySeconds = initialDelaySeconds;
    }
}