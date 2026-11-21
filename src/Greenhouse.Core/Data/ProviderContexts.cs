using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Greenhouse.Core.Data;

/// <summary>Контекст для SQLite (локальний запуск і тести). Міграції — у Data/Migrations/Sqlite.</summary>
public sealed class SqliteGreenhouseDbContext(DbContextOptions<SqliteGreenhouseDbContext> options)
    : GreenhouseDbContext(options);

/// <summary>Контекст для PostgreSQL (Docker, сервер). Міграції — у Data/Migrations/Postgres.</summary>
public sealed class PostgresGreenhouseDbContext(DbContextOptions<PostgresGreenhouseDbContext> options)
    : GreenhouseDbContext(options);

/// <summary>Для `dotnet ef migrations add --context SqliteGreenhouseDbContext`.</summary>
public sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteGreenhouseDbContext>
{
    public SqliteGreenhouseDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteGreenhouseDbContext>().UseSqlite("Data Source=design.db").Options);
}

/// <summary>Для `dotnet ef migrations add --context PostgresGreenhouseDbContext` (підключення до БД не потрібне).</summary>
public sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresGreenhouseDbContext>
{
    public PostgresGreenhouseDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PostgresGreenhouseDbContext>().UseNpgsql("Host=localhost;Database=greenhouse").Options);
}
