namespace ResilienceWithAop;

public interface ITiwazService
{
    [Resilient(maxAttempts: 5, initialDelaySeconds: 2)]
    Task<string> GetBondsAsync();
}