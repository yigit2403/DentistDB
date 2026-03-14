using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DentistDB.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseMySql(
            "Server=localhost;Database=DentistDB;User=root;Password=;",
            new MySqlServerVersion(new Version(8, 0, 0)));
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
