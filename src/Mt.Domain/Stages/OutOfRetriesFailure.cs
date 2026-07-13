using Mt.Results;

namespace Mt.Domain.Stages;

/// <summary>A retryable operation exhausted its bounded attempt budget (§6.5).</summary>
public sealed record OutOfRetriesFailure(string Message) : Failure(Message);
