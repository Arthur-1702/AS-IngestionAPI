namespace IngestionService.Models;

public class SensorReading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FieldId { get; set; }               // Id do talhão (PropertyService)
    public double SoilHumidity { get; set; }        // % umidade do solo (0–100)
    public double Temperature { get; set; }         // °C
    public double Precipitation { get; set; }       // mm
    public DateTime RecordedAt { get; set; }        // timestamp do sensor
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
