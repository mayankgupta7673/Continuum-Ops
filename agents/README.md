# Foundry Prompt Agents

This folder is the source of truth for the **Diagnosis Agent** and **Verify Agent**
Prompt Agent definitions — the two LLM reasoning components in Continuum-Ops. It
does not contain application code; it contains the instructions text, tool
manifest, and an optional provisioning script for creating these agents in a
Microsoft Foundry project.

The Repair Agent is **not** a Prompt Agent — it's plain deterministic C# code in
[src/Continuum.Ops.Functions/Activities/RepairActivities.cs](../src/Continuum.Ops.Functions/Activities/RepairActivities.cs).

## What you need to create to get the agents actually working

1. **A Foundry project** with a `gpt-4o` (or later) model deployment. See
   [docs/02-Deployment-Guide.md](../docs/02-Deployment-Guide.md).
2. **The MCP tool server deployed** ([src/mcp-server/](../src/mcp-server/)) and
   registered as a Toolbox connection in the Foundry project — this is what
   turns the 7 Python functions into callable agent tools. See
   [src/mcp-server/README.md](../src/mcp-server/README.md) for the Toolbox
   registration steps.
3. **Two Prompt Agents created in the Foundry project**:
   - `diagnosis-agent` — instructions in [diagnosis-agent/instructions.md](diagnosis-agent/instructions.md),
     tools: `peek_dlq_messages`, `query_application_logs`, `search_similar_patterns`.
   - `verify-agent` — instructions in [verify-agent/instructions.md](verify-agent/instructions.md),
     tools: `check_dlq_depth`, `query_erp`, `upsert_pattern`.
   - Either create them **portal-first** (fastest — see steps in each
     `instructions.md`) or **code-first** with [provision_agents.py](provision_agents.py).
4. **Environment variables set on the .NET Functions app** pointing at the
   provisioned agents: `DIAGNOSIS_AGENT_ID`, `VERIFY_AGENT_ID`,
   `FOUNDRY_PROJECT_ENDPOINT` (see
   [src/Continuum.Ops.Functions/local.settings.json.example](../src/Continuum.Ops.Functions/local.settings.json.example)).
5. **Managed identity RBAC**: the .NET Functions app's identity needs the
   Foundry **User** role at the project scope (to call agents) and the Python
   MCP server's tools need data-plane roles on Service Bus, Cosmos DB, AI
   Search, and Application Insights (see
   [docs/05-Security-Compliance.md](../docs/05-Security-Compliance.md)).

## ⚠️ Known gap — flagged, not guessed

[src/Continuum.Ops.Functions/Agents/FoundryAgentClient.cs](../src/Continuum.Ops.Functions/Agents/FoundryAgentClient.cs)
currently invokes the Responses API with **inline** `model` + `instructions`
on every call (sending the same instructions text as this folder, tagging
`metadata.agentId` for correlation only) rather than invoking a *named,
persisted* Prompt Agent by ID. The exact request schema for invoking a
persisted agent by ID through the Responses API was not confirmed in
available docs at the time this was written — verify against current Foundry
SDK/REST docs before relying on `DIAGNOSIS_AGENT_ID`/`VERIFY_AGENT_ID` for
anything beyond log correlation.

## Files

| File | Purpose |
|---|---|
| [diagnosis-agent/instructions.md](diagnosis-agent/instructions.md) | System instructions + portal setup steps for the Diagnosis Agent |
| [verify-agent/instructions.md](verify-agent/instructions.md) | System instructions + portal setup steps for the Verify Agent |
| [tools-manifest.json](tools-manifest.json) | Which MCP tools each agent needs attached, and why |
| [provision_agents.py](provision_agents.py) | Optional code-first agent creation using the `azure-ai-projects` SDK |
| [requirements.txt](requirements.txt) | Python deps for `provision_agents.py` |
