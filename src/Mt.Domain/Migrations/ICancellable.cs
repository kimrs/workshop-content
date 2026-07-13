using Mt.Results;

namespace Mt.Domain.Migrations;

/// <summary>
/// Implemented by every state that may be cancelled: <c>Created</c>, <c>Transformed</c>,
/// <c>PreSaftUploaded</c>, <c>SaftUploaded</c> (§6.2). Cancelling enters
/// <see cref="Cancelling"/>, seeding each unlock flag to <c>true</c> for a system that
/// was never locked (nothing to release).
/// </summary>
public interface ICancellable
{
    Result<Cancelling> Cancel();
}
