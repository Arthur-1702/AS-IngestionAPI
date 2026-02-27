using IngestionService.Data;
using IngestionService.Messaging;
using IngestionService.Models;
using Microsoft.EntityFrameworkCore;

namespace IngestionService.Services;

public class IngestionService(
    IngestionDbContext db,
    ServiceBusPublisher publisher,
    ILogger<IngestionService> logger)
{
    public async Task<SensorReadingResponse> IngestAsync(SensorReadingRequest req, CancellationToken ct = default)
    {
        var reading = MapToEntity(req);

        db.SensorReadings.Add(reading);
        await db.SaveChangesAsync(ct);

        await publisher.PublishAsync(MapToEvent(reading), ct);

        logger.LogInformation(
            "Leitura recebida e publicada. ReadingId={ReadingId} FieldId={FieldId}",
            reading.Id, reading.FieldId);

        return MapToResponse(reading);
    }

    public async Task<IEnumerable<SensorReadingResponse>> IngestBatchAsync(
        SensorReadingsBatchRequest req, CancellationToken ct = default)
    {
        var readings = req.Readings.Select(MapToEntity).ToList();

        db.SensorReadings.AddRange(readings);
        await db.SaveChangesAsync(ct);

        await publisher.PublishBatchAsync(readings.Select(MapToEvent), ct);

        logger.LogInformation("Batch de {Count} leituras ingeridas.", readings.Count);

        return readings.Select(MapToResponse);
    }

    public async Task<IEnumerable<SensorReadingResponse>> GetHistoryAsync(
        Guid fieldId, DateTime? from, DateTime? to, int limit, CancellationToken ct = default)
    {
        var query = db.SensorReadings
            .Where(r => r.FieldId == fieldId)
            .AsNoTracking();

        if (from.HasValue) query = query.Where(r => r.RecordedAt >= from.Value);
        if (to.HasValue)   query = query.Where(r => r.RecordedAt <= to.Value);

        var readings = await query
            .OrderByDescending(r => r.RecordedAt)
            .Take(Math.Min(limit, 1000))
            .ToListAsync(ct);

        return readings.Select(MapToResponse);
    }

    // ── Mappers ───────────────────────────────────────────────────────────

    private static SensorReading MapToEntity(SensorReadingRequest req) => new()
    {
        FieldId = req.FieldId,
        SoilHumidity = req.SoilHumidity,
        Temperature = req.Temperature,
        Precipitation = req.Precipitation,
        RecordedAt = req.RecordedAt ?? DateTime.UtcNow
    };

    private static SensorReadingEvent MapToEvent(SensorReading r) => new(
        r.Id, r.FieldId, r.SoilHumidity, r.Temperature, r.Precipitation, r.RecordedAt);

    private static SensorReadingResponse MapToResponse(SensorReading r) => new(
        r.Id, r.FieldId, r.SoilHumidity, r.Temperature, r.Precipitation, r.RecordedAt, r.ReceivedAt);
}
