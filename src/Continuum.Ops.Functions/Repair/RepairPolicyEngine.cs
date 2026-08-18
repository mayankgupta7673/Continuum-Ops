namespace Continuum.Ops.Functions.Repair;

/// <summary>
/// Deterministic policy gate applied before any repair action executes.
/// Static and pure (no I/O, no env var reads) so it can be called directly
/// from orchestrator code, which must be deterministic/replay-safe.
/// This is intentionally simple code, not an LLM call — see
/// docs/06-AI-Agent-Implementation.md and docs/05-Security-Compliance.md.
/// </summary>
public static class RepairPolicy
{
    public static (bool Approved, string? Reason) Evaluate(double confidence, string riskLevel, int stepCount, double minConfidence)
    {
        if (confidence < minConfidence)
        {
            return (false, $"Confidence {confidence:P0} is below the required threshold {minConfidence:P0}");
        }

        if (string.Equals(riskLevel, "high", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "High-risk repair plans always require human approval");
        }

        if (stepCount == 0)
        {
            return (false, "Repair plan has no actionable steps");
        }

        return (true, null);
    }
}
