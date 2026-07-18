using Mt.Results;

namespace Mt.Domain;

/// <summary>Lock the hero in the source system before migrating it.</summary>
public interface ILockSource
{
    Result<Response> Handle(Id migrationId);

    public abstract record Response
    {
        /// <summary>Source locked the hero.</summary>
        public sealed record Locked : Response;

        /// <summary>Source did not lock this time; the step decides whether to retry.</summary>
        public sealed record Faulted(string Reason) : Response;
    }
}
