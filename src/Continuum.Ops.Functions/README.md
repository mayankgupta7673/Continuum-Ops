# Continuum-Ops .NET Orchestrator + Repair Agent

.NET 8 isolated-worker Azure Functions app implementing:
- **Durable Functions Orchestrator** ([Orchestrators/IncidentOrchestrator.cs](Orchestrators/IncidentOrchestrator.cs)) — owns the full incident lifecycle, zero LLM calls itself.
- **Repair Agent** ([Activities/RepairActivities.cs](Activities/RepairActivities.cs)) — deterministic, policy-gated tool execution via the [MCP tool server](../mcp-server/).
- **Agent activities** ([Activities/AgentActivities.cs](Activities/AgentActivities.cs)) — the only two LLM calls in the system, invoking the Diagnosis and Verify Foundry Prompt Agents through the Responses API.
- **Approval flow** ([Activities/ApprovalActivities.cs](Activities/ApprovalActivities.cs), [Triggers/ApprovalCallbackTrigger.cs](Triggers/ApprovalCallbackTrigger.cs)) — Teams Adaptive Card + Durable Functions external events.
- **Entry point** ([Triggers/IncidentTrigger.cs](Triggers/IncidentTrigger.cs)) — Event Grid trigger from an Azure Monitor alert, one orchestration instance per `alertId`.

See [docs/06-AI-Agent-Implementation.md](../../docs/06-AI-Agent-Implementation.md) for the full build guide.

## Local development

```powershell
dotnet restore
Copy-Item local.settings.json.example local.settings.json
# Edit local.settings.json with your dev resource names, then:
func start
```

Requires an identity (via `az login` locally, managed identity in Azure) with
least-privilege data-plane roles on Cosmos DB, the Foundry project, and the
MCP tool server's system key — no connection strings or API keys are stored
in configuration.

## Deploy

```powershell
dotnet publish -c Release
func azure functionapp publish <your-dotnet-function-app-name>
```
