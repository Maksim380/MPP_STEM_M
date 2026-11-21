using Greenhouse.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Greenhouse.Core.Data;

/// <summary>
/// Модель БД у 3НФ, спільна для SQLite і PostgreSQL. Провайдер-специфічні нащадки
/// (<see cref="SqliteGreenhouseDbContext"/>, <see cref="PostgresGreenhouseDbContext"/>) мають власні міграції.
/// </summary>
public abstract class GreenhouseDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<GreenhouseEntity> Greenhouses => Set<GreenhouseEntity>();
    public DbSet<SensorType> SensorTypes => Set<SensorType>();
    public DbSet<Threshold> Thresholds => Set<Threshold>();
    public DbSet<Sensor> Sensors => Set<Sensor>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<Alert> Alerts => Set<Alert>();

    /// <summary>Дата створення стартових записів (стала, щоб міграції були детерміновані).</summary>
    private static readonly DateTime SeedDate = new(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite не зберігає DateTimeKind, а PostgreSQL вимагає UTC для timestamptz: явно тримаємо все в UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users", t => t.HasCheckConstraint("ck_users_role", "role IN ('admin', 'operator', 'viewer')"));
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Username).HasColumnName("username").HasMaxLength(64);
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(256);
            e.Property(x => x.Role).HasColumnName("role").HasMaxLength(16);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GreenhouseEntity>(e =>
        {
            e.ToTable("greenhouses");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            e.Property(x => x.Location).HasColumnName("location").HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(new GreenhouseEntity { Id = 1, Name = "Теплиця №1", Location = "Навчальна ділянка", CreatedAt = SeedDate });
        });

        modelBuilder.Entity<SensorType>(e =>
        {
            e.ToTable("sensor_types", t => t.HasCheckConstraint("ck_sensor_types_valid_range", "valid_min < valid_max"));
            e.HasKey(x => x.Kind);
            e.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(32);
            e.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(16);
            e.Property(x => x.ValidMin).HasColumnName("valid_min");
            e.Property(x => x.ValidMax).HasColumnName("valid_max");
            e.HasData(SensorCatalog.Types.Select(t => new SensorType
            {
                Kind = t.Kind,
                Unit = t.Unit,
                ValidMin = t.ValidMin,
                ValidMax = t.ValidMax,
            }));
        });

        modelBuilder.Entity<Threshold>(e =>
        {
            e.ToTable("thresholds", t => t.HasCheckConstraint("ck_thresholds_range", "comfort_min <= comfort_max"));
            e.HasKey(x => new { x.GreenhouseId, x.Kind });
            e.Property(x => x.GreenhouseId).HasColumnName("greenhouse_id");
            e.Property(x => x.Kind).HasColumnName("kind");
            e.Property(x => x.ComfortMin).HasColumnName("comfort_min");
            e.Property(x => x.ComfortMax).HasColumnName("comfort_max");
            e.HasOne(x => x.Greenhouse).WithMany(g => g.Thresholds)
                .HasForeignKey(x => x.GreenhouseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Type).WithMany()
                .HasForeignKey(x => x.Kind).OnDelete(DeleteBehavior.Restrict);
            e.HasData(SensorCatalog.Types.Select(t => new Threshold
            {
                GreenhouseId = 1,
                Kind = t.Kind,
                ComfortMin = t.ComfortMin,
                ComfortMax = t.ComfortMax,
            }));
        });

        modelBuilder.Entity<Sensor>(e =>
        {
            e.ToTable("sensors");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(64);
            e.Property(x => x.Kind).HasColumnName("kind");
            e.Property(x => x.GreenhouseId).HasColumnName("greenhouse_id");
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.GreenhouseId);
            e.HasOne(x => x.Type).WithMany(t => t.Sensors)
                .HasForeignKey(x => x.Kind).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Greenhouse).WithMany(g => g.Sensors)
                .HasForeignKey(x => x.GreenhouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasData(SensorCatalog.DefaultSensors.Select((s, i) => new Sensor
            {
                Id = i + 1,
                Name = s.Name,
                Kind = s.Kind,
                GreenhouseId = 1,
            }));
        });

        modelBuilder.Entity<Reading>(e =>
        {
            e.ToTable("readings");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SensorId).HasColumnName("sensor_id");
            e.Property(x => x.Value).HasColumnName("value");
            e.Property(x => x.RecordedAt).HasColumnName("recorded_at");
            // Індекс під основні вибірки: покази датчика за часом і останній показ.
            e.HasIndex(x => new { x.SensorId, x.RecordedAt });
            e.HasOne(x => x.Sensor).WithMany(s => s.Readings)
                .HasForeignKey(x => x.SensorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Alert>(e =>
        {
            e.ToTable("alerts", t =>
            {
                t.HasCheckConstraint("ck_alerts_kind", "kind IN ('low', 'high')");
                t.HasCheckConstraint("ck_alerts_closed_after_opened", "closed_at IS NULL OR closed_at >= opened_at");
            });
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SensorId).HasColumnName("sensor_id");
            e.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(8);
            e.Property(x => x.TriggerValue).HasColumnName("trigger_value");
            e.Property(x => x.OpenedAt).HasColumnName("opened_at");
            e.Property(x => x.ClosedAt).HasColumnName("closed_at");
            e.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at");
            e.Property(x => x.AcknowledgedBy).HasColumnName("acknowledged_by").HasMaxLength(64);
            // Пошук відкритої тривоги датчика і журнал тривог за часом.
            e.HasIndex(x => new { x.SensorId, x.ClosedAt });
            e.HasIndex(x => x.OpenedAt);
            e.HasOne(x => x.Sensor).WithMany(s => s.Alerts)
                .HasForeignKey(x => x.SensorId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? v.Value.ToUniversalTime() : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}
