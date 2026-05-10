namespace Oasis.Resilience.Actors;

using Akka.Actor;

/// <summary>
/// Message sent to an <see cref="OperationRunner"/> instructing it to execute the supplied operation
/// and reply with the result (or a <see cref="Status.Failure"/> on exception).
/// </summary>
/// <param name="Operation">The asynchronous operation to execute.</param>
public sealed record RunOperation(Func<Task<object>> Operation);

/// <summary>
/// A long-lived, stateless Akka.NET actor that executes <see cref="RunOperation"/> messages on demand.
/// A single instance can be reused for any operation, enabling the proxy to cache supervisors per
/// method without leaking actors across invocations.
/// </summary>
public sealed class OperationRunner : ReceiveActor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OperationRunner"/> class.
    /// </summary>
    public OperationRunner()
    {
        ReceiveAsync<RunOperation>(HandleExecute);
    }

    /// <summary>Executes the wrapped operation and sends the result or failure back to the originating sender.</summary>
    private async Task HandleExecute(RunOperation msg)
    {
        var sender = Sender;
        try
        {
            var result = await msg.Operation();
            sender.Tell(result);
        }
        catch (Exception ex)
        {
            sender.Tell(new Status.Failure(ex));
        }
    }
}
