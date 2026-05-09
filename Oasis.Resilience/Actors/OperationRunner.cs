namespace Oasis.Resilience.Actors;

using Akka.Actor;

/// <summary>
/// Message sent to an <see cref="OperationRunner"/> to execute the wrapped operation.
/// </summary>
public sealed record RunOperation;

/// <summary>
/// An Akka.NET actor that wraps a single operation delegate and executes it on demand.
/// </summary>
public sealed class OperationRunner : ReceiveActor
{
    private readonly Func<Task<object>> _operation;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationRunner"/> class.
    /// </summary>
    /// <param name="operation">The operation delegate to execute.</param>
    public OperationRunner(Func<Task<object>> operation)
    {
        _operation = operation;
        ReceiveAsync<RunOperation>(HandleExecute);
    }

    /// <summary>Executes the wrapped operation and sends the result or failure back to the sender.</summary>
    private async Task HandleExecute(RunOperation msg)
    {
        try
        {
            var result = await _operation();
            Sender.Tell(result);
        }
        catch (Exception ex)
        {
            Sender.Tell(new Status.Failure(ex));
        }
    }
}
