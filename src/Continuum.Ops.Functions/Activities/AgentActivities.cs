using System.Text.Json;
using Continuum.Ops.Functions.Agents;
using Continuum.Ops.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Continuum.Ops.Functions.Activities;

/// <summary>
/// Activity functions that call the Diagnosis and Verify Foundry Prompt Agents.
/// These are the only two LLM calls in the whole incident lifecycle.
/// </summary>
public class AgentActivities
{
    private readonly IFoundryAgentClient _agentClient;
    private readonly ILogger<AgentActivities> _logger;

    public AgentActivities(IFoundryAgentClient agentClient, ILogger<AgentActivities> logger)
    {
        _agentClient = agentClient;
        _logger = logger;
    }

    [Function(nameof(RunDiagnosisAgent))]
    public async Task<DiagnosisResult> RunDiagnosisAgent([ActivityTrigger] IncidentAlert alert)
    {
        _logger.LogInformation("Invoking Diagnosis Agent for alert {AlertId}", alert.AlertId);

        var agentId = Environment.GetEnvironmentVariable("DIAGNOSIS_AGENT_ID") ?? "diagnosis-agent";
        var userInput = JsonSerializer.Serialize(alert);

        var instructions = """
            You are the Diagnosis Agent for Continuum-Ops. Given a Service Bus dead-letter
            alert, use your tools (peek_dlq_messages, query_application_logs,
            search_similar_patterns) to gather evidence, identify the root cause, and
            propose a repair plan. Respond with strict JSON matching:
            {"rootCause": string, "confidence": number (0-1), "riskLevel": "low"|"medium"|"high",
             "evidenceCitations": string[], "repairPlan": [{"action": string, "description": string, "parameters": object}]}
            """;

        var responseText = await _agentClient.InvokeAsync(
            agentId, instructions, modelDeploymentName: "gpt-4o", userInput: userInput);

        return JsonSerializer.Deserialize<DiagnosisResult>(responseText)
            ?? throw new InvalidOperationException("Diagnosis Agent returned an unparsable response");
    }

    [Function(nameof(RunVerifyAgent))]
    public async Task<VerificationResult> RunVerifyAgent([ActivityTrigger] VerifyAgentInput input)
    {
        _logger.LogInformation("Invoking Verify Agent for alert {AlertId}", input.Alert.AlertId);

        var agentId = Environment.GetEnvironmentVariable("VERIFY_AGENT_ID") ?? "verify-agent";
        var userInput = JsonSerializer.Serialize(input);

        var instructions = """
            You are the Verify Agent for Continuum-Ops. A repair action has just been executed.
            Use your tools (check_dlq_depth, query_erp) to confirm the business outcome was
            achieved, then call upsert_pattern to record what you learned. Respond with strict
            JSON matching: {"verified": boolean, "evidence": string, "failureReason": string|null}
            """;

        var responseText = await _agentClient.InvokeAsync(
            agentId, instructions, modelDeploymentName: "gpt-4o", userInput: userInput);

        return JsonSerializer.Deserialize<VerificationResult>(responseText)
            ?? throw new InvalidOperationException("Verify Agent returned an unparsable response");
    }
}

public record VerifyAgentInput(IncidentAlert Alert, DiagnosisResult Diagnosis, RepairResult Repair);
