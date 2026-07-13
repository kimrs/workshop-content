namespace Mt.Results;

/// <summary>
/// Base type for every failure a <see cref="Result{T}"/> can carry. Mt.Results
/// defines only the failures its own combinators construct (spec 2 D3); do not
/// add domain-specific ones here.
/// </summary>
public abstract record Failure(string Message);
