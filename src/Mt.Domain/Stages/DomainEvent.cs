using Mt.Domain.Stages.LocksSource;
using Mt.Domain.Stages.LocksTarget;
using Mt.Domain.Stages.Transforms;
using Mt.Domain.Stages.TriggersExport;
using Mt.Domain.Stages.UnlocksSource;
using Mt.Domain.Stages.UnlocksTarget;
using Mt.Domain.Stages.UploadsPreSaft;
using Mt.Domain.Stages.UploadsSaft;
using Mt.Results;

namespace Mt.Domain.Stages;

/// <summary>
/// Base type for the message that drives every stage transition. Each slice declares
/// its own subtype with a stable <see cref="ToString"/> (§6.4). The name on the wire
/// is <c>ToString()</c>; <see cref="FromString"/> reverses it via a safe lookup —
/// never <c>Single()</c> (§10).
/// </summary>
public abstract record DomainEvent
{
    /// <summary>The closed set of events, used by <see cref="FromString"/>.</summary>
    private static readonly IReadOnlyList<DomainEvent> Known =
    [
        new SourceLockRequested(),
        new TargetLockRequested(),
        new ExportRequested(),
        new TransformRequested(),
        new PreSaftUploadRequested(),
        new SaftUploadRequested(),
        new SourceUnlockRequested(),
        new TargetUnlockRequested(),
    ];

    public abstract override string ToString();

    public static Result<DomainEvent> FromString(string name) =>
        Known
            .FirstOrDefault(e => e.ToString() == name)
            .EnsureFound($"Unknown domain event '{name}'.");
}
