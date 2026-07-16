using Mt.DistinctComics;
using Mt.Domain;

var distinctComics = new DistinctComicsLockSource();

Console.WriteLine($"Distinct Comics answered: {distinctComics.Handle(42)}");
new Handler(distinctComics).Handle(42);
