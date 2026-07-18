using Mt.Results;

namespace Mt.Domain;

public interface INotifyCompletion
{
    Result<ValueTuple> Handle(Request request);

    public closed record Request(Id MigrationId)
    {
        public sealed record Migrated(Id MigrationId) : Request(MigrationId);

        public sealed record Cancelled(Id MigrationId) : Request(MigrationId);
    }
}
