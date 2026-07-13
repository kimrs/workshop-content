using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mt.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations</c> construct the context at design time (§11, §13). The
/// connection string here is only used to build the model, not to connect during scaffolding.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<WorkshopDbContext>
{
    public WorkshopDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WorkshopDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=workshop;Username=workshop;Password=workshop")
            .Options;

        return new WorkshopDbContext(options);
    }
}
