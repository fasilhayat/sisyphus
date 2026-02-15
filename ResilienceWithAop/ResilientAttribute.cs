namespace ResilienceWithAop;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ResilientAttribute : Attribute
{
    public int MaxAttempts { get; }
    public int InitialDelaySeconds { get; }

    public ResilientAttribute(int maxAttempts = 5, int initialDelaySeconds = 2)
    {
        MaxAttempts = maxAttempts;
        InitialDelaySeconds = initialDelaySeconds;
    }
}