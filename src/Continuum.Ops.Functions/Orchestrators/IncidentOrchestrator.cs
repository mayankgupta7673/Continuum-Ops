using Continuum.Ops.Functions.Activities;
using Continuum.Ops.Functions.Models;
using Continuum.Ops.Functions.Repair;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Continuum.Ops.Functions.Orchestrators;

/// <summary>
/// Owns the full incident lifecycle: diagnose -> policy gate -> (optional human
/// approval) -> repair -> verify -> persist. Zero LLM calls happen in this file —
/// it only calls the two agent activities and otherwise runs deterministic code.
/// </summary>
public class IncidentOrchestrator
{
    private static readonly TimeSpan ApprovalTimeout = TimeSpan.FromHours(4);

    [Function(nameof(RunIncidentOrchestrator))]
    public async Task<IncidentRecord> RunIncidentOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var alert = context.GetInput<IncidentAlert>()
            ?? throw new InvalidOperationException("Orchestrator started without an IncidentAlert input");

        var logger = context.CreateReplaySafeLogger<IncidentOrchestrator>();
        logger.LogInformation("Starting incident orchestration for alert {AlertId}", alert.AlertId);

        var record = new IncidentRecord(
            Id: alert.AlertId, TenantId: alert.TenantId, AlertId: alert.AlertId,
            Status: "Diagnosing", Diagnosis: null, Repair: null, Verification: null,
            CreatedAtUtc: context.CurrentUtcDateTime, UpdatedAtUtc: context.CurrentUtcDateTime);

        // App settings are read once, up front, via an activity — orchestrator
        // code itself must stay deterministic/replay-safe and must not call
        // Environment.GetEnvironmentVariable directly.
        var config = await context.CallActivityAsync<OrchestratorConfig>(nameof(ConfigActivities.GetOrchestratorConfig), string.Empty);

        // 1. Diagnosis Agent (1 LLM call)
        var diagnosis = await context.CallActivityAsync<DiagnosisResult>(nameof(AgentActivities.RunDiagnosisAgent), alert);
        record = record with { Status = "Diagnosed", Diagnosis = diagnosis, UpdatedAtUtc = context.CurrentUtcDateTime };
        await context.CallActivityAsync(nameof(PersistenceActivities.SaveIncidentRecord), record);

        // 2. Deterministic policy gate
        var (approved, reason) = RepairPolicy.Evaluate(diagnosis.Confidence, diagnosis.RiskLevel, diagnosis.RepairPlan.Count, config.RepairMinConfidence);

        if (!approved)
        {
            logger.LogInformation("Repair plan for {AlertId} requires human approval: {Reason}", alert.AlertId, reason);

            await context.CallActivityAsync(nameof(ApprovalActivities.RequestApproval), new ApprovalRequest(
                alert, diagnosis, context.InstanceId, ApprovalCallbackBaseUrl: config.ApprovalCallbackBaseUrl));

            using var cts = new CancellationTokenSource();
            var approvalTask = context.WaitForExternalEvent<bool>("ApprovalReceived", cts.Token);
            var timeoutTask = context.CreateTimer(context.CurrentUtcDateTime.Add(ApprovalTimeout), cts.Token);

            var winner = await Task.WhenAny(approvalTask, timeoutTask);
            cts.Cancel();

            if (winner != approvalTask || !approvalTask.Result)
            {
                logger.LogInformation("Repair plan for {AlertId} was not approved (timeout or rejection)", alert.AlertId);
                return record with { Status = "AwaitingApproval-TimedOutOrRejected", UpdatedAtUtc = context.CurrentUtcDateTime };
            }
        }

        // 3. Repair Agent (0 LLM calls)
        var repair = await context.CallActivityAsync<RepairResult>(
            nameof(RepairActivities.ExecuteRepairPlan), new RepairPlanInput(alert, diagnosis));
        record = record with { Status = repair.Succeeded ? "Repaired" : "RepairFailed", Repair = repair, UpdatedAtUtc = context.CurrentUtcDateTime };
        await context.CallActivityAsync(nameof(PersistenceActivities.SaveIncidentRecord), record);

        if (!repair.Succeeded)
        {
            logger.LogWarning("Repair failed for {AlertId}: {Errors}", alert.AlertId, string.Join("; ", repair.Errors));
            return record;
        }

        // 4. Verify Agent (1 LLM call, conditional on repair success)
        var verification = await context.CallActivityAsync<VerificationResult>(
            nameof(AgentActivities.RunVerifyAgent), new VerifyAgentInput(alert, diagnosis, repair));

        record = record with
        {
            Status = verification.Verified ? "Verified" : "VerificationFailed",
            Verification = verification,
            UpdatedAtUtc = context.CurrentUtcDateTime,
        };
        await context.CallActivityAsync(nameof(PersistenceActivities.SaveIncidentRecord), record);

        return record;
    }
}
