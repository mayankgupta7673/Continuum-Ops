namespace Continuum.Ops.Functions.Models;

public record IncidentAlert(
    string AlertId,
    string TenantId,
    string Namespace,
    string Queue,
    int DeadLetterMessageCount,
    DateTimeOffset DetectedAtUtc);

public record RepairPlanStep(string Action, string Description, Dictionary<string, object> Parameters);

public record DiagnosisResult(
    string RootCause,
    double Confidence,
    string RiskLevel,
    List<string> EvidenceCitations,
    List<RepairPlanStep> RepairPlan);

public record RepairResult(bool Succeeded, int ActionsExecuted, List<string> Errors);

public record VerificationResult(bool Verified, string Evidence, string? FailureReason);

public record IncidentRecord(
    string Id,
    string TenantId,
    string AlertId,
    string Status,
    DiagnosisResult? Diagnosis,
    RepairResult? Repair,
    VerificationResult? Verification,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
