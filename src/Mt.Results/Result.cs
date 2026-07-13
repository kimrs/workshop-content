namespace Mt.Results;

/// <summary>
/// The result of a fallible operation: either <see cref="Completed"/> with a
/// value or <see cref="Failed"/> with one or more failures (§5.1, spec 10).
/// </summary>
public abstract record Result<T>
{
    public sealed record Completed(T Value) : Result<T>;

    public sealed record Failed : Result<T>
    {
        public Failed(IEnumerable<Failure> failures)
        {
            Failures = [.. failures];
            if (Failures.Count == 0)
            {
                throw new ArgumentException("A failed result requires at least one failure.", nameof(failures));
            }
        }

        // Get-only so a `with` expression cannot bypass the non-empty guard (spec 12 D11).
        public IReadOnlyList<Failure> Failures { get; }
    }

    public static implicit operator Result<T>(T value) => new Completed(value);

    public static implicit operator Result<T>(Failure failure) => new Failed([failure]);

    public static implicit operator Result<T>(Failure[] failures) => new Failed(failures);
}
