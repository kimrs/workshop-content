using Mt.Results;

namespace Mt.Domain;

/// <summary>Lock the hero's new home in the target system before migrating it.</summary>
public interface ILockTarget
{
    Result<Response> Handle(Id migrationId);

    public abstract record Response
    {
        /// <summary>Target locked the hero.</summary>
        public sealed record Locked : Response;

        /// <summary>Target did not lock this time; the step decides whether to retry.</summary>
        public sealed record Faulted(string Reason) : Response;
    }
}
