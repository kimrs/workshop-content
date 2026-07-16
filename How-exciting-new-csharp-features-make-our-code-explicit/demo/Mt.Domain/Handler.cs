namespace Mt.Domain;

/// <summary>Locks the source, then advances the migration. The opening slide.</summary>
public sealed class Handler(ILockSource lockSource)
{
    public void Handle(long migrationId)
    {
        var action = lockSource.Handle(migrationId) switch
        {
            ILockSource.Response.Locked => $"✅ Source locked — advancing migration {migrationId} to Transform",
            ILockSource.Response.Faulted(var reason) => $"⏰ Lock faulted ({reason}) — scheduling retry",
            _ => throw new ArgumentOutOfRangeException(nameof(migrationId), migrationId, null),
        };

        Console.WriteLine(action);
    }
}
