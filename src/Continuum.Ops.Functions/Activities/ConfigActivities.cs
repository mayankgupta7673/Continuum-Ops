using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Continuum.Ops.Functions.Activities;

public record OrchestratorConfig(double RepairMinConfidence, string ApprovalCallbackBaseUrl);

/// <summary>
/// Reads app settings on behalf of the orchestrator. Orchestrator code must be
/// deterministic/replay-safe, so environment variable reads happen here (in an
/// activity, which Durable Functions caches per-execution) rather than inline
/// in <see cref="Orchestrators.IncidentOrchestrator"/>.
/// </summary>
public class ConfigActivities
{
    [Function(nameof(GetOrchestratorConfig))]
    public Task<OrchestratorConfig> GetOrchestratorConfig([ActivityTrigger] string _)
    {
        var minConfidence = double.TryParse(
            Environment.GetEnvironmentVariable("REPAIR_MIN_CONFIDENCE"), out var configured)
            ? configured
            : 0.7;

        var approvalCallbackBaseUrl = Environment.GetEnvironmentVariable("APPROVAL_CALLBACK_BASE_URL") ?? "";

        return Task.FromResult(new OrchestratorConfig(minConfidence, approvalCallbackBaseUrl));
    }
}
