using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DentistDB.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Read connection string from environment variable at design time.
        // Set DENTISTDB_CONNECTION before running EF Core migrations:
        //   export DENTISTDB_CONNECTION="Server=...;Database=...;User=...;Password=...;"
        var connectionString =
            Environment.GetEnvironmentVariable("DENTISTDB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set the DENTISTDB_CONNECTION environment variable with a valid MySQL connection string before running EF Core migrations.");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21)));
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
