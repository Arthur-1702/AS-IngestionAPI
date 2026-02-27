using System.ComponentModel.DataAnnotations;

namespace IngestionService.Models;

public record SensorReadingRequest(
    [Required] Guid FieldId,
    [Range(0, 100)] double SoilHumidity,
    [Range(-50, 60)] double Temperature,
    [Range(0, 500)] double Precipitation,
    DateTime? RecordedAt                // se nulo, usa DateTime.UtcNow
);

public record SensorReadingResponse(
    Guid Id,
    Guid FieldId,
    double SoilHumidity,
    double Temperature,
    double Precipitation,
    DateTime RecordedAt,
    DateTime ReceivedAt
);

public record SensorReadingsBatchRequest(
    [Required][MinLength(1)][MaxLength(100)]
    IEnumerable<SensorReadingRequest> Readings
);

// ── Evento publicado no Azure Service Bus ─────────────────────────────────
public record SensorReadingEvent(
    Guid ReadingId,
    Guid FieldId,
    double SoilHumidity,
    double Temperature,
    double Precipitation,
    DateTime RecordedAt
);
