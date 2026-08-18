# Continuum-Ops MCP Tool Server (Python)

Remote MCP server exposing evidence and repair tools to the Diagnosis and
Verify Foundry Prompt Agents, built on the Azure Functions Python v2
programming model and the `mcp_tool_trigger` binding.

See [docs/06-AI-Agent-Implementation.md](../../docs/06-AI-Agent-Implementation.md) for the full build guide.

## Tools exposed

| Tool | Type | Used by |
|---|---|---|
| `peek_dlq_messages` | read-only | Diagnosis Agent |
| `query_application_logs` | read-only | Diagnosis Agent |
| `search_similar_patterns` | read-only | Diagnosis Agent |
| `check_dlq_depth` | read-only | Verify Agent |
| `query_erp` | read-only | Verify Agent |
| `replay_messages` | mutating | Repair workflow (post-approval) |
| `upsert_pattern` | mutating | Verify Agent |

## Local development

```powershell
python -m venv .venv
.venv\Scripts\Activate.ps1
pip install -r requirements.txt

Copy-Item local.settings.json.example local.settings.json
# Edit local.settings.json with your dev resource names, then:
func start
```

Requires Azure Functions Core Tools 4.0.7030+ and an identity (via `az login`
locally, managed identity in Azure) with least-privilege data-plane roles on
Service Bus, AI Search, Application Insights, and the ERP API — no
connection strings or API keys are used.

## Deploy

```powershell
func azure functionapp publish <your-python-function-app-name>
```

After deploying, register `https://<app>.azurewebsites.net/runtime/webhooks/mcp`
as a custom MCP server in the Foundry portal's Add Tools catalog (using the
function's `mcp_extension` system key), then publish it to the project's
Toolbox so both agents share the same governed tool set.
