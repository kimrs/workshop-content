using Mt.Results;

namespace Mt.Domain.Migrations;

/// <summary>
/// An illegal state transition was attempted on a <see cref="Migration"/>. This is a
/// domain concept and lives here, not in the generic result library — the mistake of
/// leaking such a type into <c>Mt.Results</c> is exactly what §10 warns against.
/// The API maps it to 409 (§9).
/// </summary>
public sealed record MigrationHasIncorrectState(string Message) : Failure(Message);
