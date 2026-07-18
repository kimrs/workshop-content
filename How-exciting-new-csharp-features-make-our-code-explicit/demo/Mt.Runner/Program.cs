using Mt.DistinctComics;
using Mt.Domain;
using Mt.Marble;

var migrationId = Id.Create(42);

var distinctComics = new DistinctComicsLockSource();
var marbleLock = new LockTarget();
var marbleNotify = new NotifyCompletion();

Console.WriteLine($"Distinct Comics answered: {distinctComics.Handle(migrationId).Value}");
Console.WriteLine($"Marble answered: {marbleLock.Handle(migrationId).Value}");
marbleNotify.Handle(new INotifyCompletion.Request.Migrated(migrationId));

new Handler(distinctComics, marbleLock, marbleNotify).Handle(migrationId);
