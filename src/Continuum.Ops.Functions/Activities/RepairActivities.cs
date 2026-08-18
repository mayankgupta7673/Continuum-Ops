using Continuum.Ops.Functions.Mcp;
using Continuum.Ops.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Continuum.Ops.Functions.Activities;

/// <summary>
/// The Repair Agent: deterministic, policy-gated execution of the plan
/// proposed by the Diagnosis Agent. Zero LLM calls.
/// </summary>
public class RepairActivities
{
    private readonly IMcpToolClient _mcpClient;
    private readonly ILogger<RepairActivities> _logger;

    public RepairActivities(IMcpToolClient mcpClient, ILogger<RepairActivities> logger)
    {
        _mcpClient = mcpClient;
        _logger = logger;
    }

    [Function(nameof(ExecuteRepairPlan))]
    public async Task<RepairResult> ExecuteRepairPlan([ActivityTrigger] RepairPlanInput input)
    {
        var errors = new List<string>();
        var executed = 0;

        foreach (var step in input.Diagnosis.RepairPlan)
        {
            try
            {
                switch (step.Action)
                {
                    case "replay_messages":
                        var maxMessages = int.TryParse(
                            Environment.GetEnvironmentVariable("REPAIR_MAX_MESSAGES_PER_RUN"), out var configured)
                            ? configured : 10;

                        await _mcpClient.CallToolAsync("replay_messages", new
                        {
                            queue = input.Alert.Queue,
                            count = maxMessages,
                        });
                        executed++;
                        break;

                    default:
                        _logger.LogWarning("Unrecognized repair action {Action} — skipping", step.Action);
                        errors.Add($"Unrecognized action: {step.Action}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Repair step {Action} failed for alert {AlertId}", step.Action, input.Alert.AlertId);
                errors.Add($"{step.Action}: {ex.Message}");
            }
        }

        return new RepairResult(Succeeded: errors.Count == 0 && executed > 0, ActionsExecuted: executed, Errors: errors);
    }
}

public record RepairPlanInput(IncidentAlert Alert, DiagnosisResult Diagnosis);
