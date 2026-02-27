using IngestionService.Models;
using IngestionService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IngestionService.Controllers;

[ApiController]
[Route("sensor-data")]
[Authorize]
public class SensorDataController(IngestionService.Services.IngestionService ingestionService) : ControllerBase
{
    /// <summary>
    /// Recebe uma leitura de sensor para um talhão.
    /// Persiste no banco e publica no Azure Service Bus.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SensorReadingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ingest(
        [FromBody] SensorReadingRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await ingestionService.IngestAsync(req, ct);
        return Accepted(result);
    }

    /// <summary>
    /// Recebe um lote de leituras de sensor (até 100 por chamada).
    /// Útil para simuladores que acumulam dados antes de enviar.
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(IEnumerable<SensorReadingResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IngestBatch(
        [FromBody] SensorReadingsBatchRequest req,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await ingestionService.IngestBatchAsync(req, ct);
        return Accepted(result);
    }

    /// <summary>
    /// Retorna o histórico de leituras de um talhão.
    /// Usado pelo dashboard de monitoramento.
    /// </summary>
    [HttpGet("{fieldId:guid}/history")]
    [ProducesResponseType(typeof(IEnumerable<SensorReadingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        Guid fieldId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 200,
        CancellationToken ct = default)
    {
        var result = await ingestionService.GetHistoryAsync(fieldId, from, to, limit, ct);
        return Ok(result);
    }

    /// <summary>Verifica saúde do serviço.</summary>
    [AllowAnonymous]
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "healthy", service = "IngestionService" });
}
