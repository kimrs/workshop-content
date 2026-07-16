using Mt.Domain.Steps.LocksSource;
using Mt.Domain.Steps.LocksTarget;
using Mt.Domain.Steps.Transforms;
using Mt.Domain.Steps.TriggersExport;
using Mt.Domain.Steps.UnlocksSource;
using Mt.Domain.Steps.UnlocksTarget;
using Mt.Domain.Steps.UploadsPreSaft;
using Mt.Domain.Steps.UploadsSaft;
using Mt.Results;

namespace Mt.Domain.Steps;

/// <summary>
/// Base type for the message that drives every step transition. Each slice declares
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
