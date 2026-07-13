using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Stages;
using Mt.Persistence;
using Mt.Results;
using Mt.Transport;

namespace Mt.Processor;

/// <summary>
/// Consumes envelopes, rebuilds the routing tuple, restores the trace context, and runs each through
/// the inbox <c>ExecuteOnce</c> (§8.5). A failed message is logged and skipped — it never crashes the loop.
/// </summary>
public sealed partial class ProcessorWorker(
    IMessageConsumer consumer,
    IServiceScopeFactory scopeFactory,
    ILogger<ProcessorWorker> logger) : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("Mt.Processor");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("⚙️ Processor started.");
        try
        {
            // ProcessAsync never throws (failures are logged and recorded by the inbox), so
            // returning from it acknowledges the message — the abort semantics (spec 12 D2).
            await consumer.ConsumeAsync(ProcessAsync, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
    }

    private async Task ProcessAsync(MessageEnvelope envelope, CancellationToken ct)
    {
        var parsed = ParseEnvelope(envelope);
        if (parsed.IsFailed(out var message, out var parseFailures))
        {
            Log.MalformedEnvelope(logger, envelope.DomainEvent, parseFailures[0].Message);
            return;
        }

        using var activity = StartActivity(envelope.TraceParent, message.domainEvent);

        await using var scope = scopeFactory.CreateAsyncScope();
        var executeOnce = scope.ServiceProvider.GetRequiredService<ExecuteOnce>();

        var eventName = message.domainEvent.ToMessageType();
        var result = await executeOnce.ExecuteOnceAsync(message.id, message.domainEvent, message.attempt, ct);
        if (result.IsFailed(out _, out var failures))
        {
            Log.MessageFailed(logger, eventName, message.id.Value, message.attempt.Value, failures[0].Message);
        }
        else
        {
            Log.MessageProcessed(logger, eventName, message.id.Value, message.attempt.Value);
        }
    }

    private static Result<(Id id, DomainEvent domainEvent, Attempt attempt)> ParseEnvelope(MessageEnvelope envelope) =>
        Id.Create(envelope.MigrationId)
            .Then(id => DomainEvent.FromString(envelope.DomainEvent)
                .Then(domainEvent => Attempt.Create(envelope.Attempt)
                    .Then(attempt => (id, domainEvent, attempt))));

    private static Activity? StartActivity(string? traceParent, DomainEvent domainEvent)
    {
        var name = domainEvent.ToMessageType();
        return ActivityContext.TryParse(traceParent, null, out var parentContext)
            ? ActivitySource.StartActivity(name, ActivityKind.Consumer, parentContext)
            : ActivitySource.StartActivity(name);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Error,
            Message = "Discarding malformed envelope for {Event}: {Reason}")]
        public static partial void MalformedEnvelope(ILogger logger, string @event, string reason);

        [LoggerMessage(Level = LogLevel.Error,
            Message = "Message {Event} for migration {MigrationId} attempt {Attempt} failed: {Reason}")]
        public static partial void MessageFailed(ILogger logger, string @event, long migrationId, int attempt, string reason);

        [LoggerMessage(Level = LogLevel.Debug,
            Message = "Processed {Event} for migration {MigrationId} attempt {Attempt}.")]
        public static partial void MessageProcessed(ILogger logger, string @event, long migrationId, int attempt);
    }
}
