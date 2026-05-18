namespace Oasis.Resilience.Proxies;

using Actors;
using Akka.Actor;
using Akka.Pattern;
using Attributes;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// A <see cref="DispatchProxy"/> that intercepts method calls and applies resilience policies (retry,
/// circuit breaker, supervision, fan-out) based on attributes applied to the target method. Sentinel
/// values (<c>-1</c>) on attribute parameters fall back to the configured global options. Supervisor
/// actors are cached per method (for supervision) and per worker type (for fan-out) to avoid leaking
/// actors across invocations.
/// </summary>
/// <typeparam name="T">The interface type to proxy.</typeparam>
public class ResilientProxy<T> : DispatchProxy, IAsyncDisposable, IDisposable
{
    private static readonly ConcurrentDictionary<MethodInfo, RetryAttribute?> RetryAttributeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, CircuitBreakerAttribute?> CircuitBreakerAttributeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, SupervisionAttribute?> SupervisionAttributeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, FanOutAttribute?> FanOutAttributeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, MethodInfo> ImplementedMethodCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, MethodInfo> InvokeGenericMethodCache = new();

    private readonly ConcurrentDictionary<MethodInfo, Lazy<IActorRef>> _supervisorCache = new();

    /// <summary>
    /// Gets or sets the decorated service instance that method calls are forwarded to.
    /// </summary>
    public T DecoratedInstance { get; set; } = default!;

    /// <summary>
    /// Gets or sets the actor ref for retry operations.
    /// </summary>
    public IActorRef ResilienceActorRef { get; set; } = default!;

    /// <summary>
    /// Gets or sets the actor ref for circuit breaker operations.
    /// </summary>
    public IActorRef CircuitBreakerActorRef { get; set; } = default!;

    /// <summary>
    /// Gets or sets the actor system used to create supervised and worker actors.
    /// </summary>
    public ActorSystem ActorSystem { get; set; } = default!;

    /// <summary>
    /// Gets or sets global retry options used as defaults when attributes omit values.
    /// </summary>
    public RetryOptions? RetryOptions { get; set; }

    /// <summary>
    /// Gets or sets global circuit breaker options used as defaults when attributes omit values.
    /// </summary>
    public CircuitBreakerOptions? CircuitBreakerOptions { get; set; }

    /// <summary>
    /// Gets or sets global supervision options used as defaults when attributes omit values.
    /// </summary>
    public SupervisionOptions? SupervisionOptions { get; set; }

    /// <summary>
    /// Gets or sets global fan-out options used as defaults when attributes omit values.
    /// </summary>
    public FanOutOptions? FanOutOptions { get; set; }

    /// <summary>
    /// Intercepts the method invocation, resolves resilience attributes, and delegates to the appropriate handler.
    /// </summary>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        ArgumentNullException.ThrowIfNull(args);

        var method = ResolveImplementedMethod(targetMethod);

        var retryAttr = RetryAttributeCache.GetOrAdd(method, m => m.GetCustomAttribute<RetryAttribute>());
        var breakerAttr = CircuitBreakerAttributeCache.GetOrAdd(method, m => m.GetCustomAttribute<CircuitBreakerAttribute>());
        var supervisionAttr = SupervisionAttributeCache.GetOrAdd(method, m => m.GetCustomAttribute<SupervisionAttribute>());
        var fanOutAttr = FanOutAttributeCache.GetOrAdd(method, m => m.GetCustomAttribute<FanOutAttribute>());

        var safeArgs = args ?? [];

        if (!HasResilienceAttributes(retryAttr, breakerAttr, supervisionAttr, fanOutAttr))
            return targetMethod.Invoke(DecoratedInstance, safeArgs);

        return InvokeResilient(method, safeArgs, retryAttr, breakerAttr, supervisionAttr, fanOutAttr);
    }

    /// <summary>Determines whether any resilience attributes are present on the method.</summary>
    private static bool HasResilienceAttributes(RetryAttribute? retry, CircuitBreakerAttribute? breaker, SupervisionAttribute? supervision, FanOutAttribute? fanOut)
        => retry is not null || breaker is not null || supervision is not null || fanOut is not null;

    /// <summary>Resolves the implemented method from the decorated instance matching the target method signature.
    /// Cached per <see cref="MethodInfo"/> so the reflection lookup runs once per interface method.</summary>
    private MethodInfo ResolveImplementedMethod(MethodInfo targetMethod)
    {
        return ImplementedMethodCache.GetOrAdd(targetMethod, tm =>
        {
            var method = DecoratedInstance!.GetType().GetMethod(tm.Name,
                tm.GetParameters().Select(p => p.ParameterType).ToArray());
            return method ?? throw new InvalidOperationException($"Implementation method not found: {tm.Name}");
        });
    }

    /// <summary>
    /// Routes the invocation to the generic resilience pipeline, resolving the return type and invoking
    /// <see cref="InvokeGeneric{TResult}"/> via reflection. The constructed generic <see cref="MethodInfo"/>
    /// is cached per implemented method to avoid the cost of <c>MakeGenericMethod</c> on every call.
    /// </summary>
    private object InvokeResilient(MethodInfo implementedMethod, object?[]? args, RetryAttribute? retryAttr, CircuitBreakerAttribute? breakerAttr, SupervisionAttribute? supervisionAttr, FanOutAttribute? fanOutAttr)
    {
        var method = InvokeGenericMethodCache.GetOrAdd(implementedMethod, BuildInvokeGeneric);
        return method.Invoke(this, [implementedMethod, args, retryAttr, breakerAttr, supervisionAttr, fanOutAttr])!;
    }

    /// <summary>Builds the generic <c>InvokeGeneric&lt;TResult&gt;</c> <see cref="MethodInfo"/> for the implemented method.</summary>
    private static MethodInfo BuildInvokeGeneric(MethodInfo implementedMethod)
    {
        var returnType = implementedMethod.ReturnType;
        if (!returnType.IsGenericType)
            throw new InvalidOperationException($"Method '{implementedMethod.Name}' on '{implementedMethod.DeclaringType?.Name}' has unsupported return type '{returnType.Name}'. Only Task<T> is supported.");

        var resultType = returnType.GetGenericArguments()[0];
        return typeof(ResilientProxy<T>).GetMethod(nameof(InvokeGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(resultType);
    }

    /// <summary>Invokes the resilience pipeline for a given return type, dispatching to fan-out or wrapping operation with supervision as configured.</summary>
    private async Task<TResult> InvokeGeneric<TResult>(MethodInfo implementedMethod, object[] args, RetryAttribute? retryAttr, CircuitBreakerAttribute? breakerAttr, SupervisionAttribute? supervisionAttr, FanOutAttribute? fanOutAttr)
    {
        var ct = ExtractCancellationToken(args, implementedMethod);

        if (fanOutAttr is not null)
            return await HandleFanOut<TResult>(implementedMethod, args, fanOutAttr);

        var operation = CreateOperation<TResult>(implementedMethod, args);

        if (supervisionAttr is not null)
            operation = WrapWithSupervision(implementedMethod, operation, supervisionAttr);

        return await ExecuteResilienceStrategy<TResult>(operation, implementedMethod, breakerAttr, retryAttr, supervisionAttr, ct);
    }

    /// <summary>Extracts the <see cref="CancellationToken"/> from the method arguments, if present.</summary>
    private static CancellationToken ExtractCancellationToken(object[] args, MethodInfo method)
    {
        var parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(CancellationToken) && args[i] is CancellationToken token)
                return token;
        }
        return CancellationToken.None;
    }

    /// <summary>Returns the configured ask timeout, falling back to a 30s default when no options are configured.</summary>
    private TimeSpan AskTimeout => RetryOptions?.AskTimeout ?? TimeSpan.FromSeconds(30);

    /// <summary>Dispatches the operation to the appropriate resilience strategy based on configured attributes.</summary>
    private async Task<TResult> ExecuteResilienceStrategy<TResult>(Func<Task<object>> operation, MethodInfo implementedMethod, CircuitBreakerAttribute? breakerAttr, RetryAttribute? retryAttr, SupervisionAttribute? supervisionAttr, CancellationToken ct)
    {
        var operationKey = $"{typeof(T).FullName}.{implementedMethod.Name}";

        if (breakerAttr is not null)
            return await ExecuteWithCircuitBreaker<TResult>(operationKey, operation, breakerAttr, retryAttr, ct);

        if (retryAttr is not null)
            return await ExecuteWithRetry<TResult>(operation, retryAttr, ct, operationKey);

        if (supervisionAttr is not null)
            return (TResult)await operation();

        throw new InvalidOperationException($"No resilience attributes configured on method '{implementedMethod.Name}' of '{implementedMethod.DeclaringType?.Name}'.");
    }

    /// <summary>Creates a delegate that invokes the method and returns its typed result as an object.
    /// Unwraps <see cref="TargetInvocationException"/> so the original exception type reaches the
    /// resilience pipeline (and the caller).</summary>
    private Func<Task<object>> CreateOperation<TResult>(MethodInfo implementedMethod, object[] args)
    {
        return async () =>
        {
            object? result;
            try
            {
                result = implementedMethod.Invoke(DecoratedInstance, args);
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                throw tie.InnerException;
            }

            if (result is null) throw new InvalidOperationException($"Method '{implementedMethod.Name}' returned null, but a Task<T> was expected.");

            var task = (Task<TResult>)result;
            return (await task)!;
        };
    }

    /// <summary>Executes the operation through the circuit breaker actor, falling back to retry on failure if configured.</summary>
    private async Task<TResult> ExecuteWithCircuitBreaker<TResult>(string operationKey, Func<Task<object>> operation, CircuitBreakerAttribute breakerAttr, RetryAttribute? retryAttr, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var resolved = OptionsResolver.ResolveCircuitBreaker(breakerAttr, CircuitBreakerOptions);

        try
        {
            var breakerResult = await CircuitBreakerActorRef.Ask<object>(
                new CircuitBreakerActor.ExecuteWithBreaker(
                    operationKey,
                    operation,
                    resolved.FailureThreshold,
                    TimeSpan.FromMilliseconds(resolved.ResetTimeoutMs),
                    resolved.MaxConcurrentCalls),
                AskTimeout,
                ct);

            return (TResult)breakerResult!;
        }
        catch (CircuitBreakerActor.CircuitBreakerOpenException)
        {
            throw;
        }
        catch (Exception ex) when (retryAttr is not null)
        {
            return await HandleCircuitBreakerFailure<TResult>(ex, operation, retryAttr, ct, operationKey);
        }
    }

    /// <summary>Handles a circuit breaker failure by delegating to the retry strategy.</summary>
    private async Task<TResult> HandleCircuitBreakerFailure<TResult>(Exception cause, Func<Task<object>> operation, RetryAttribute retryAttr, CancellationToken ct, string operationKey)
    {
        _ = cause;
        return await ExecuteWithRetry<TResult>(operation, retryAttr, ct, operationKey);
    }

    /// <summary>Executes the operation with retry logic via the retry actor, honoring the resolved retry options and exception filter.</summary>
    private async Task<TResult> ExecuteWithRetry<TResult>(Func<Task<object>> operation, RetryAttribute retryAttr, CancellationToken ct, string operationKey = "")
    {
        ct.ThrowIfCancellationRequested();

        var resolved = OptionsResolver.ResolveRetry(retryAttr, RetryOptions);

        var result = await ResilienceActorRef.Ask<object>(
            new RetryActor.Execute(
                operation,
                resolved.MaxAttempts,
                TimeSpan.FromMilliseconds(resolved.InitialDelayMs),
                resolved.RetryOn,
                operationKey),
            AskTimeout,
            ct);

        if (result is Status.Failure f) throw f.Cause;

        return (TResult)result!;
    }

    /// <summary>
    /// Wraps the operation in a supervised actor that restarts on failure according to the supervision strategy.
    /// The supervisor is cached per method, so repeated invocations reuse the same long-lived actor.
    /// </summary>
    private Func<Task<object>> WrapWithSupervision(MethodInfo method, Func<Task<object>> operation, SupervisionAttribute supervision)
    {
        var supervisor = _supervisorCache.GetOrAdd(method, m =>
            new Lazy<IActorRef>(() => CreateSupervisedRunner(m, supervision), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        return async () =>
        {
            var result = await supervisor.Ask<object>(new RunOperation(operation), AskTimeout);

            if (result is Status.Failure failure)
                throw failure.Cause;

            return result;
        };
    }

    /// <summary>Creates a long-lived supervisor actor for the supplied method using the resolved supervision options.</summary>
    private IActorRef CreateSupervisedRunner(MethodInfo method, SupervisionAttribute supervision)
    {
        var resolved = OptionsResolver.ResolveSupervision(supervision, SupervisionOptions);
        var props = SupervisionActor.CreateSupervisorProps(
            resolved.Strategy, resolved.MaxRetries, resolved.BackoffMinMs, resolved.BackoffMaxMs, resolved.RandomFactor);
        var actorName = $"supervised-{typeof(T).Name}-{method.Name}-{Guid.NewGuid():N}";
        return ActorSystem.ActorOf(props, actorName);
    }

    /// <summary>
    /// Orchestrates a fan-out operation by invoking the implementation once per item in the split
    /// array parameter and merging the partial results. Concurrency is capped by <paramref name="fanOut"/>
    /// <c>MaxWorkers</c>; all items are always processed regardless of array size.
    /// </summary>
    private async Task<TResult> HandleFanOut<TResult>(MethodInfo method, object[] args, FanOutAttribute fanOut)
    {
        var maxWorkers = OptionsResolver.ResolveMaxWorkers(fanOut, FanOutOptions);
        var parameters = method.GetParameters();
        var splitIndex = FindSplitParameterIndex(parameters, fanOut.SplitOn);
        var splitArray = (Array)args[splitIndex];

        var gate = new SemaphoreSlim(Math.Max(1, maxWorkers), Math.Max(1, maxWorkers));
        var tasks = new List<Task<TResult>>(splitArray.Length);

        for (int i = 0; i < splitArray.Length; i++)
            tasks.Add(InvokeForSingleItem<TResult>(method, args, splitIndex, splitArray.GetValue(i)!, gate));

        _ = DisposeGateWhenComplete(tasks, gate);

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return AggregateBuiltIn<TResult>(results);
    }

    /// <summary>
    /// Resolves which parameter index to split on. When <paramref name="splitOn"/> is <c>null</c>,
    /// auto-detects the single array parameter on the method.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the named parameter is not found, no array parameter exists, or multiple array
    /// parameters are present and <paramref name="splitOn"/> was not specified.
    /// </exception>
    private static int FindSplitParameterIndex(ParameterInfo[] parameters, string? splitOn)
    {
        if (splitOn is not null)
        {
            var named = Array.FindIndex(parameters, p => p.Name == splitOn);
            if (named == -1)
                throw new InvalidOperationException(
                    $"[FanOut] split parameter '{splitOn}' not found. Verify the 'splitOn' value matches the parameter name exactly.");
            return named;
        }

        var arrayParams = Array.FindAll(parameters, p => p.ParameterType.IsArray);
        if (arrayParams.Length == 0)
            throw new InvalidOperationException(
                "[FanOut] could not auto-detect a split parameter: no array parameter found. " +
                "Add an array parameter or specify 'splitOn' in [FanOut].");
        if (arrayParams.Length > 1)
            throw new InvalidOperationException(
                $"[FanOut] found {arrayParams.Length} array parameters and cannot auto-detect which to split. " +
                "Specify 'splitOn' in [FanOut].");

        return Array.FindIndex(parameters, p => p.Name == arrayParams[0].Name);
    }

    /// <summary>
    /// Waits for a slot in <paramref name="gate"/>, invokes the implementation with a single-element
    /// array replacing the split parameter, and releases the slot when done.
    /// </summary>
    private async Task<TResult> InvokeForSingleItem<TResult>(MethodInfo method, object[] args, int splitIndex, object item, SemaphoreSlim gate)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var elementType = args[splitIndex].GetType().GetElementType()!;
            var singleItemArray = Array.CreateInstance(elementType, 1);
            singleItemArray.SetValue(item, 0);

            var callArgs = (object[])args.Clone();
            callArgs[splitIndex] = singleItemArray;

            object? result;
            try
            {
                result = method.Invoke(DecoratedInstance, callArgs);
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                throw tie.InnerException;
            }

            if (result is null)
                throw new InvalidOperationException($"[FanOut] method '{method.Name}' returned null for a single-item invocation.");

            return await ((Task<TResult>)result).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Merges <paramref name="partialResults"/> into a single <typeparamref name="TResult"/>.
    /// Supported return types: <c>Dictionary&lt;TKey,TValue&gt;</c> (entries merged),
    /// <c>T[]</c> (elements concatenated), <c>List&lt;T&gt;</c> (elements concatenated).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TResult"/> is not a supported aggregation type.
    /// </exception>
    private static TResult AggregateBuiltIn<TResult>(TResult[] partialResults)
    {
        if (partialResults.Length == 0) return default!;
        if (partialResults.Length == 1) return partialResults[0];

        var resultType = typeof(TResult);

        if (typeof(IDictionary).IsAssignableFrom(resultType))
            return MergeDictionaries<TResult>(partialResults, resultType);

        if (resultType.IsArray)
            return ConcatArrays<TResult>(partialResults, resultType);

        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(List<>))
            return ConcatLists<TResult>(partialResults, resultType);

        throw new InvalidOperationException(
            $"[FanOut] cannot automatically aggregate return type '{resultType.Name}'. " +
            "Supported types: Dictionary<TKey,TValue>, T[], List<T>.");
    }

    /// <summary>Merges partial dictionary results into a single dictionary instance.</summary>
    private static TResult MergeDictionaries<TResult>(TResult[] partialResults, Type resultType)
    {
        var merged = (IDictionary)Activator.CreateInstance(resultType)!;
        foreach (var partial in partialResults)
            foreach (DictionaryEntry entry in (IDictionary)(object)partial!)
                merged[entry.Key] = entry.Value;
        return (TResult)(object)merged;
    }

    /// <summary>Concatenates partial array results into a single array.</summary>
    private static TResult ConcatArrays<TResult>(TResult[] partialResults, Type resultType)
    {
        var elementType = resultType.GetElementType()!;
        var all = partialResults.Cast<IEnumerable>().SelectMany(e => e.Cast<object?>()).ToArray();
        var arr = Array.CreateInstance(elementType, all.Length);
        for (int i = 0; i < all.Length; i++) arr.SetValue(all[i], i);
        return (TResult)(object)arr;
    }

    /// <summary>Concatenates partial list results into a single list.</summary>
    private static TResult ConcatLists<TResult>(TResult[] partialResults, Type resultType)
    {
        var list = (IList)Activator.CreateInstance(resultType)!;
        foreach (var partial in partialResults)
            foreach (var item in (IEnumerable)(object)partial!)
                list.Add(item);
        return (TResult)(object)list;
    }

    /// <summary>Disposes the throttling gate once all fan-out tasks have settled.</summary>
    private static async Task DisposeGateWhenComplete<TResult>(List<Task<TResult>> tasks, SemaphoreSlim gate)
    {
        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch { /* individual failures surface through the awaited Task.WhenAll on the caller */ }
        finally { gate.Dispose(); }
    }

    /// <summary>Synchronous disposal that delegates to <see cref="DisposeAsync"/>; provided so the
    /// proxy is compatible with synchronous DI scope disposal.</summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
        GC.SuppressFinalize(this);
    }

    /// <summary>Stops every cached supervisor actor and clears the per-instance caches.
    /// The shared <see cref="ResilienceActorRef"/> and <see cref="CircuitBreakerActorRef"/> are owned
    /// by the <c>ResilienceRuntime</c> and intentionally not stopped here.</summary>
    public async ValueTask DisposeAsync()
    {
        await StopCachedActorsAsync(_supervisorCache.Values).ConfigureAwait(false);
        _supervisorCache.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>Gracefully stops every materialised actor in <paramref name="lazyRefs"/>.</summary>
    private async Task StopCachedActorsAsync(ICollection<Lazy<IActorRef>> lazyRefs)
    {
        if (ActorSystem is null) return;

        foreach (var lazy in lazyRefs)
        {
            if (!lazy.IsValueCreated) continue;
            try { await lazy.Value.GracefulStop(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch { /* swallow — disposal is best-effort */ }
        }
    }
}
