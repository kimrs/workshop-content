namespace Mt.Domain;

/// <summary>
/// Named event ids for the milestones the workshop narrates through logs (§4.10, §13).
/// Shared across the stage handlers — these are constants, not the slice logic that §4.3 keeps
/// duplicated. Const ints so they can be used in <c>[LoggerMessage]</c> attributes (spec 2 §5);
/// they convert implicitly to <see cref="Microsoft.Extensions.Logging.EventId"/>.
/// </summary>
public static class LogEvents
{
    public const int StageSkippedCancelled = 1001;

    public const int StageAlreadyDone = 1002;

    public const int StageRetryScheduled = 1003;

    public const int StageOutOfRetries = 1004;

    public const int FanInAdvanced = 1005;

    public const int TransformAutoCancelled = 1006;

    public const int MigrationFinalized = 1007;

    public const int InboxDuplicateSkipped = 1008;

    public const int RetriesPromoted = 1009;

    public const int MigrationStarted = 1010;

    public const int MigrationApproved = 1011;

    public const int MigrationCancelRequested = 1012;

    public const int InboxAbortRecorded = 1013;
}
