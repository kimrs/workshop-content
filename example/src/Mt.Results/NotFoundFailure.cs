namespace Mt.Results;

/// <summary>A lookup found nothing where something was expected.</summary>
public sealed record NotFoundFailure(string Message) : Failure(Message);
