using Mt.Domain;

namespace Mt.DistinctComics;

/// <summary>
/// Distinct Comics' connector for their own system. They know their API rate-limits, so they
/// helpfully report throttling as its own response — a perfectly reasonable thing to do,
/// and nothing in the type system stops them.
/// </summary>
public sealed record Throttled(TimeSpan RetryAfter) : ILockSource.Response;

public sealed class DistinctComicsLockSource : ILockSource
{
    public ILockSource.Response Handle(long migrationId) => new Throttled(TimeSpan.FromMinutes(5));
}
