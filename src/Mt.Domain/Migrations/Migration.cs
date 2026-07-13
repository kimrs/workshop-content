using Mt.Results;

namespace Mt.Domain.Migrations;

/// <summary>
/// The migration aggregate: an abstract root with one sealed subclass per state,
/// forming a discriminated union (§6.2). Every transition is a method returning
/// <c>Result&lt;NextState&gt;</c>; an illegal transition returns
/// <see cref="MigrationHasIncorrectState"/>. There is no <c>null</c> anywhere and no
/// in-place mutation — each transition produces a new state value. One exception to
/// the method rule: <c>Created → Prepared</c> is performed by the setup flag-set
/// persistence ops, guarded on <see cref="Created.IsReadyToTransform"/> (spec 9 D2).
/// </summary>
public abstract record Migration(Id Id);

/// <summary>
/// Initial state. Holds the three setup fan-in flags; becomes <see cref="Prepared"/>
/// once all three are set. Cancelling seeds each unlock flag from what was actually
/// locked.
/// </summary>
public sealed record Created(
    Id Id,
    OrganizationNumber OrganizationNumber,
    bool SourceLocked,
    bool TargetLocked,
    bool ExportTriggered) : Migration(Id), ICancellable
{
    public bool IsReadyToTransform => SourceLocked && TargetLocked && ExportTriggered;

    // Seed each unlock flag to true when that system was never locked: there is
    // nothing to release, so teardown for it is already complete.
    public Result<Cancelling> Cancel() =>
        new Cancelling(Id, OrganizationNumber, SourceUnlocked: !SourceLocked, TargetUnlocked: !TargetLocked);
}

/// <summary>
/// Setup complete: all three fan-in flags were set. Entered by whichever setup stage
/// lands the last flag — the flag-set persistence op performs the transition (spec 9
/// D2), so there is no <c>Prepare()</c> here. Ready for Transform.
/// </summary>
public sealed record Prepared(Id Id, OrganizationNumber OrganizationNumber)
    : Migration(Id), ICancellable
{
    public Result<Transformed> Transform() => new Transformed(Id, OrganizationNumber);

    // Everything was locked on the way in, so both systems need unlocking.
    public Result<Cancelling> Cancel() =>
        new Cancelling(Id, OrganizationNumber, SourceUnlocked: false, TargetUnlocked: false);
}

/// <summary>Client data validated; ready to upload the pre-SAF-T work.</summary>
public sealed record Transformed(Id Id, OrganizationNumber OrganizationNumber)
    : Migration(Id), ICancellable
{
    public Result<PreSaftUploaded> UploadPreSaft() => new PreSaftUploaded(Id, OrganizationNumber);

    // Past Created, both systems are locked, so both must be unlocked on cancel.
    public Result<Cancelling> Cancel() =>
        new Cancelling(Id, OrganizationNumber, SourceUnlocked: false, TargetUnlocked: false);
}

/// <summary>Pre-SAF-T work uploaded; ready to upload the SAF-T file.</summary>
public sealed record PreSaftUploaded(Id Id, OrganizationNumber OrganizationNumber)
    : Migration(Id), ICancellable
{
    public Result<SaftUploaded> UploadSaft() => new SaftUploaded(Id, OrganizationNumber);

    public Result<Cancelling> Cancel() =>
        new Cancelling(Id, OrganizationNumber, SourceUnlocked: false, TargetUnlocked: false);
}

/// <summary>SAF-T uploaded; parked until a user approves (§1.3).</summary>
public sealed record SaftUploaded(Id Id, OrganizationNumber OrganizationNumber)
    : Migration(Id), ICancellable
{
    public Result<Unlocking> Approve() =>
        new Unlocking(Id, OrganizationNumber, SourceUnlocked: false, TargetUnlocked: false);

    public Result<Cancelling> Cancel() =>
        new Cancelling(Id, OrganizationNumber, SourceUnlocked: false, TargetUnlocked: false);
}

/// <summary>Approved teardown. Holds the two unlock fan-in flags.</summary>
public sealed record Unlocking(
    Id Id,
    OrganizationNumber OrganizationNumber,
    bool SourceUnlocked,
    bool TargetUnlocked) : Migration(Id)
{
    public bool IsFullyUnlocked => SourceUnlocked && TargetUnlocked;

    public Result<Completed> Complete() =>
        IsFullyUnlocked
            ? new Completed(Id, OrganizationNumber)
            : new MigrationHasIncorrectState(
                $"Migration {Id.Value} cannot complete before both systems are unlocked.");
}

/// <summary>Terminal success.</summary>
public sealed record Completed(Id Id, OrganizationNumber OrganizationNumber) : Migration(Id);

/// <summary>Cancellation teardown. Holds the two unlock fan-in flags, like <see cref="Unlocking"/>.</summary>
public sealed record Cancelling(
    Id Id,
    OrganizationNumber OrganizationNumber,
    bool SourceUnlocked,
    bool TargetUnlocked) : Migration(Id)
{
    public bool IsFullyUnlocked => SourceUnlocked && TargetUnlocked;

    public Result<Cancelled> FinalizeCancellation() =>
        IsFullyUnlocked
            ? new Cancelled(Id, OrganizationNumber)
            : new MigrationHasIncorrectState(
                $"Migration {Id.Value} cannot finalize cancellation before both systems are unlocked.");
}

/// <summary>Terminal cancellation. The organization number may be re-migrated (§1.4).</summary>
public sealed record Cancelled(Id Id, OrganizationNumber OrganizationNumber) : Migration(Id);
