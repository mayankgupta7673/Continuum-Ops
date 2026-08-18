# Continuum-Ops Infrastructure (Bicep)

Deploys the shared platform: Storage, Application Insights/Log Analytics,
Cosmos DB (`incidents` + `patterns` containers), Azure AI Search, Azure
OpenAI (`gpt-4o` deployment), a Premium (EP1/EP2) Function App plan, and the
two Function Apps (Python MCP tool server, .NET orchestrator) with
system-assigned managed identities and least-privilege RBAC.

**Not included** (provisioned/configured separately):
- The Foundry **project** and **Prompt Agents** — see [agents/](../../agents/).
- The Service Bus namespace(s) being monitored — usually pre-existing and
  often cross-subscription; see the RBAC steps in
  [docs/02-Deployment-Guide.md](../../docs/02-Deployment-Guide.md).

## Deploy

```powershell
az group create --name rg-continuumops-dev --location eastus

az deployment group create `
  --resource-group rg-continuumops-dev `
  --template-file main.bicep `
  --parameters environmentName=dev location=eastus openAiDeploymentName=gpt-4o
```

Then follow [docs/02-Deployment-Guide.md](../../docs/02-Deployment-Guide.md) to
grant the Function Apps' managed identities access to your Service Bus
namespace, and [agents/README.md](../../agents/README.md) to create the
Foundry Prompt Agents and register the MCP Toolbox.
