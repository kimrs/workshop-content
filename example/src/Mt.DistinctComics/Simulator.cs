using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Mt.Domain.ExternalIds;
using Mt.Results;

namespace Mt.DistinctComics;

/// <summary>
/// The core of every simulated Source operation: decide success/failure as a function of config
/// + the simulator's own call count, log what it "did", and return a <see cref="Result{T}"/>
/// (§7, spec 7). Like a real flaky external system, it keeps its own per-operation counter and
/// never sees the caller's retry state — and it knows the client only by punch card (spec 8).
/// Registered as a singleton so the count survives across message scopes.
/// </summary>
public sealed partial class Simulator
{
    private readonly ConcurrentDictionary<(string Operation, string PunchCard), int> _calls = new();

    public Result<ValueTuple> Run(
        OperationFailure config,
        string successLog,
        string operation,
        IdValue punchCard,
        ILogger logger)
    {
        var call = _calls.AddOrUpdate((operation, punchCard.Value), 1, (_, count) => count + 1);
        var fails = config.AlwaysFail
            || (config.FailUntilAttempt is int until && call < until);

        if (!fails)
        {
            Log.Succeeded(logger, successLog, punchCard.Value, call);
            return default(ValueTuple);
        }

        Log.Failed(logger, operation, punchCard.Value, call);

        if (config.Throw)
        {
            return new OperationThrew($"{operation} threw for {punchCard.Value}.");
        }

        return new OperationFailed(
            $"{operation} failed for {punchCard.Value} on call {call}.");
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "{SuccessLog} for {PunchCard} (call {Call}).")]
        public static partial void Succeeded(ILogger logger, string successLog, string punchCard, int call);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "💥 [SIM] {Operation} failed for {PunchCard} on call {Call} (configured).")]
        public static partial void Failed(ILogger logger, string operation, string punchCard, int call);
    }
}
