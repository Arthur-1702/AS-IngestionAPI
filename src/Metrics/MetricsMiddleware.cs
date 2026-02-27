using Prometheus;

namespace IngestionService.Metrics;

public class MetricsMiddleware(RequestDelegate next)
{
    private static readonly Counter ReadingsIngested = Prometheus.Metrics
        .CreateCounter("ingestion_readings_total", "Total de leituras de sensor recebidas",
            new CounterConfiguration { LabelNames = ["result"] });

    private static readonly Counter BatchesIngested = Prometheus.Metrics
        .CreateCounter("ingestion_batches_total", "Total de batches recebidos");

    private static readonly Histogram RequestDuration = Prometheus.Metrics
        .CreateHistogram("ingestion_http_request_duration_seconds", "Duração das requisições HTTP",
            new HistogramConfiguration { LabelNames = ["method", "path", "status"] });

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await next(context);

        stopwatch.Stop();

        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;
        var statusCode = context.Response.StatusCode;
        var status = statusCode.ToString();

        RequestDuration
            .WithLabels(method, SanitizePath(path), status)
            .Observe(stopwatch.Elapsed.TotalSeconds);

        if (method == "POST" && statusCode == 202)
        {
            if (path.EndsWith("/batch", StringComparison.OrdinalIgnoreCase))
                BatchesIngested.Inc();
            else if (path.Contains("/sensor-data", StringComparison.OrdinalIgnoreCase))
                ReadingsIngested.WithLabels("success").Inc();
        }
        else if (method == "POST" && statusCode >= 400)
        {
            ReadingsIngested.WithLabels("error").Inc();
        }
    }

    private static string SanitizePath(string path) =>
        System.Text.RegularExpressions.Regex.Replace(
            path,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            "{id}"
        );
}
