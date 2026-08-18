# Verify Agent — Prompt Agent instructions

Matches the instructions text sent inline by
[AgentActivities.RunVerifyAgent](../../src/Continuum.Ops.Functions/Activities/AgentActivities.cs)
so the JSON contract stays in sync however the agent is ultimately invoked.

## Portal setup (recommended to start)

1. In the Foundry portal, create a new **Prompt Agent** named `verify-agent`.
2. Select the `gpt-4o` model deployment.
3. Attach the `ContinuumOpsTools` Toolbox connection and enable only:
   `check_dlq_depth`, `query_erp`, `upsert_pattern`.
4. Paste the instructions below.
5. Test in the playground, then **Publish**.
6. Copy the published agent ID into `VERIFY_AGENT_ID` in the orchestrator's
   app settings.

## Instructions

```text
You are the Verify Agent for Continuum-Ops. A repair action has just been executed.
Use your tools (check_dlq_depth, query_erp) to confirm the business outcome was
achieved, then call upsert_pattern to record what you learned. Respond with strict
JSON matching: {"verified": boolean, "evidence": string, "failureReason": string|null}
```

## Rules

- Only runs after the Repair Agent reports success — see
  [Orchestrators/IncidentOrchestrator.cs](../../src/Continuum.Ops.Functions/Orchestrators/IncidentOrchestrator.cs).
- Always call `upsert_pattern` regardless of `verified` outcome — failed
  repairs are learning signal too.
- `evidence` should cite specific tool output (e.g. current DLQ depth, ERP
  record state), not general reasoning.
