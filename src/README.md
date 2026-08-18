# Continuum-Ops Source

| Component | Language | Purpose |
|---|---|---|
| [mcp-server/](mcp-server/) | Python (Azure Functions) | MCP tool server — evidence + repair tools for the Diagnosis and Verify Prompt Agents |
| [Continuum.Ops.Functions/](Continuum.Ops.Functions/) | .NET 8 (Azure Functions, isolated worker) | Durable Functions orchestrator, deterministic Repair Agent, agent-invocation activities |

Foundry Prompt Agents themselves (Diagnosis Agent, Verify Agent) are not
application code — they're configured via the Foundry portal/SDK/REST. See
[agents/](../agents/) for their instructions text and provisioning script.

See [docs/06-AI-Agent-Implementation.md](../docs/06-AI-Agent-Implementation.md)
for the full build guide and [docs/08-AIOps-Solution-Architecture-Review.md](../docs/08-AIOps-Solution-Architecture-Review.md)
for why the stack is split this way (Python for the MCP server, .NET for
deterministic orchestration/repair).
