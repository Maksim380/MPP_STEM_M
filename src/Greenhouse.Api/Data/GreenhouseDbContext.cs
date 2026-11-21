using Greenhouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Greenhouse.Api.Data;

/// <summary>
/// Схема БД у 3НФ: sensor_types → sensors → readings / alerts.
/// Діапазони зберігаються один раз у типі датчика, а не дублюються в кожному датчику.
/// </summary>
public sealed class GreenhouseDbContext(DbContextOptions<GreenhouseDbContext> options) : DbContext(options)
{
    public DbSet<SensorType> SensorTypes => Set<SensorType>();
    public DbSet<Sensor> Sensors => Set<Sensor>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite не зберігає DateTimeKind, тож після читання явно позначаємо час як UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SensorType>(e =>
        {
            e.ToTable("sensor_types", t =>
            {
                t.HasCheckConstraint("ck_sensor_types_valid_range", "valid_min < valid_max");
                t.HasCheckConstraint("ck_sensor_types_comfort_range", "comfort_min <= comfort_max");
            });
            e.HasKey(x => x.Kind);
            e.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(32);
            e.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(16);
            e.Property(x => x.ValidMin).HasColumnName("valid_min");
            e.Property(x => x.ValidMax).HasColumnName("valid_max");
            e.Property(x => x.ComfortMin).HasColumnName("comfort_min");
            e.Property(x => x.ComfortMax).HasColumnName("comfort_max");
            e.HasData(SensorCatalog.Types.Select(t => new SensorType
            {
                Kind = t.Kind,
                Unit = t.Unit,
                ValidMin = t.ValidMin,
                ValidMax = t.ValidMax,
                ComfortMin = t.ComfortMin,
                ComfortMax = t.ComfortMax,
            }));
        });

        modelBuilder.Entity<Sensor>(e =>
        {
            e.ToTable("sensors");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(64);
            e.Property(x => x.Kind).HasColumnName("kind");
            e.HasIndex(x => x.Name).IsUnique();
            e.HasOne(x => x.Type).WithMany(t => t.Sensors)
                .HasForeignKey(x => x.Kind).OnDelete(DeleteBehavior.Restrict);
            e.HasData(SensorCatalog.DefaultSensors.Select((s, i) => new Sensor
            {
                Id = i + 1,
                Name = s.Name,
                Kind = s.Kind,
            }));
        });

        modelBuilder.Entity<Reading>(e =>
        {
            e.ToTable("readings");
            e.HasKey(x => x.Id);
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
            e.HasKey(x => x.Id);
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
