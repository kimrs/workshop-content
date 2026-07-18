namespace Mt.Results;

public union Result<T>(Completed<T>, Failed);
public sealed record Completed<T>(T Value);
public sealed record Failed(string Reason);
