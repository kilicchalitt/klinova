using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Klinova.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef</c> to create the SQLite context when adding migrations.</summary>
internal sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
{
    public SqliteAppDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<SqliteAppDbContext>()
            .UseSqlite("Data Source=design-time.db", o => o.MigrationsAssembly(typeof(SqliteAppDbContext).Assembly.FullName))
            .Options);
}

/// <summary>Used by <c>dotnet ef</c> to create the SQL Server context when adding migrations.</summary>
internal sealed class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerAppDbContext>
{
    public SqlServerAppDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<SqlServerAppDbContext>()
            .UseSqlServer("Server=(local);Database=Klinova.DesignTime;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
