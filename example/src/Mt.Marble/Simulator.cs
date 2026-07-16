using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Mt.Domain.ExternalIds;
using Mt.Results;

namespace Mt.Marble;

/// <summary>
/// The core of every simulated Target operation (§7, spec 7): success/failure as a function of
/// config + the simulator's own call count — it never sees the caller's retry state, and it
/// knows the tenant only by holo-crystal (spec 8). Registered as a singleton so the count
/// survives across message scopes. Independent near-duplicate of the Source simulator (§4.3).
/// </summary>
public sealed partial class Simulator
{
    private readonly ConcurrentDictionary<(string Operation, string HoloCrystal), int> _calls = new();

    public Result<ValueTuple> Run(
        OperationFailure config,
        string successLog,
        string operation,
        IdValue holoCrystal,
        ILogger logger)
    {
        var call = _calls.AddOrUpdate((operation, holoCrystal.Value), 1, (_, count) => count + 1);
        var fails = config.AlwaysFail
            || (config.FailUntilAttempt is int until && call < until);

        if (!fails)
        {
            Log.Succeeded(logger, successLog, holoCrystal.Value, call);
            return default(ValueTuple);
        }

        Log.Failed(logger, operation, holoCrystal.Value, call);

        if (config.Throw)
        {
            return new OperationThrew($"{operation} threw for {holoCrystal.Value}.");
        }

        return new OperationFailed(
            $"{operation} failed for {holoCrystal.Value} on call {call}.");
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "{SuccessLog} for {HoloCrystal} (call {Call}).")]
        public static partial void Succeeded(ILogger logger, string successLog, string holoCrystal, int call);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "💥 [SIM] {Operation} failed for {HoloCrystal} on call {Call} (configured).")]
        public static partial void Failed(ILogger logger, string operation, string holoCrystal, int call);
    }
}
