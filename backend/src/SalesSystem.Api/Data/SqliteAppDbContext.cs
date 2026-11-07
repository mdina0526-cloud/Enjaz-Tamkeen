using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SalesSystem.Api.Data;

/// <summary>
/// Same model as <see cref="AppDbContext"/>, used when Database:Provider is Sqlite (automated tests and quick local trials).
/// It exists only so SQLite gets its own migrations in Data/SqliteMigrations: data survives schema upgrades on both providers.
/// </summary>
public class SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : AppDbContext(options);

// Design-time factories: one per context so `dotnet ef --context ...` never builds the PostgreSQL migrations with the SQLite
// provider (EF would otherwise reuse a factory of the derived type for AppDbContext). No connection is opened.

public class PostgresAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=sales_system;Username=design_time").Options);
}

public class SqliteAppDbContextFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
{
    public SqliteAppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteAppDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
