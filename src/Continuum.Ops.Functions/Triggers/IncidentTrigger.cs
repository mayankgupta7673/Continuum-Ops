using System.Text.Json;
using Continuum.Ops.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Continuum.Ops.Functions.Triggers;

/// <summary>
/// Entry point: an Azure Monitor alert (via Event Grid, Common Alert Schema)
/// fans out here and starts one orchestration instance per alert, using the
/// alert ID as the orchestration instance ID for automatic deduplication.
///
/// Bound as a raw JSON string (rather than a typed Azure.Messaging.EventGrid.EventGridEvent)
/// to keep this scaffold dependency-light; swap in the typed SDK model if preferred.
/// </summary>
public class IncidentTrigger
{
    private readonly ILogger<IncidentTrigger> _logger;

    public IncidentTrigger(ILogger<IncidentTrigger> logger)
    {
        _logger = logger;
    }

    [Function(nameof(OnIncidentAlert))]
    public async Task OnIncidentAlert(
        [EventGridTrigger] string eventGridEventJson,
        [DurableClient] DurableTaskClient durableClient)
    {
        var alert = ParseAlert(eventGridEventJson);

        _logger.LogInformation("Received alert {AlertId} for {Namespace}/{Queue}", alert.AlertId, alert.Namespace, alert.Queue);

        // Using alertId as the instance ID makes starting an orchestration for an
        // already-running alert a safe no-op (idempotent under retried/duplicate events).
        await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(Orchestrators.IncidentOrchestrator.RunIncidentOrchestrator),
            alert,
            new StartOrchestrationOptions(InstanceId: alert.AlertId));
    }

    private static IncidentAlert ParseAlert(string eventGridEventJson)
    {
        // Azure Monitor Common Alert Schema payload shape — adjust the property
        // paths below to match your actual alert payload / alert rule configuration.
        using var doc = JsonDocument.Parse(eventGridEventJson);
        var root = doc.RootElement;
        var eventId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : Guid.NewGuid().ToString();

        var data = root.GetProperty("data");
        var essentials = data.GetProperty("essentials");
        var hasAlertContext = data.TryGetProperty("alertContext", out var alertContext);

        string GetString(string name) =>
            hasAlertContext && alertContext.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";

        int GetInt(string name) =>
            hasAlertContext && alertContext.TryGetProperty(name, out var v) ? v.GetInt32() : 0;

        return new IncidentAlert(
            AlertId: essentials.TryGetProperty("alertId", out var aid) ? aid.GetString() ?? eventId! : eventId!,
            TenantId: Environment.GetEnvironmentVariable("DEFAULT_TENANT_ID") ?? "default",
            Namespace: GetString("namespace"),
            Queue: GetString("queue"),
            DeadLetterMessageCount: GetInt("deadLetterMessageCount"),
            DetectedAtUtc: DateTimeOffset.UtcNow);
    }
}

