using IngestionService.Models;
using Microsoft.EntityFrameworkCore;

namespace IngestionService.Data;

public class IngestionDbContext(DbContextOptions<IngestionDbContext> options) : DbContext(options)
{
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SensorReading>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.FieldId);
            e.HasIndex(r => r.RecordedAt);
            e.HasIndex(r => new { r.FieldId, r.RecordedAt }); // consultas de histórico
            e.Property(r => r.SoilHumidity).HasPrecision(5, 2);
            e.Property(r => r.Temperature).HasPrecision(5, 2);
            e.Property(r => r.Precipitation).HasPrecision(6, 2);
        });
    }
}
