# Continuum-Ops Documentation Index

Read in this order if you're new to the project.

## Start Here
| # | Document | Purpose |
|---|---|---|
| 00 | [Product-Overview.md](00-Product-Overview.md) | Vision, value proposition, agent architecture at a glance |
| 08 | [AIOps-Solution-Architecture-Review.md](08-AIOps-Solution-Architecture-Review.md) | ⭐ Solution-architect review: open-source landscape, Datadog-style collector pattern, Foundry Agents vs. custom-build decision — read this to understand *why* the architecture looks the way it does |

## Technical Reference
| # | Document | Purpose |
|---|---|---|
| 01 | [Technical-Architecture.md](01-Technical-Architecture.md) | Full system design: sequence diagrams, data architecture, security, cost |
| 02 | [Deployment-Guide.md](02-Deployment-Guide.md) | 30-minute deployment playbook |
| 03 | [User-Manual.md](03-User-Manual.md) | Day-to-day operations guide |
| 04 | [API-Reference.md](04-API-Reference.md) | REST APIs and webhooks |
| 05 | [Security-Compliance.md](05-Security-Compliance.md) | Zero-trust architecture, audit trail, data protection |
| 06 | [AI-Agent-Implementation.md](06-AI-Agent-Implementation.md) | Step-by-step build guide: Prompt Agents + MCP tool server |
| 07 | [Ticketing-Integration-Strategy.md](07-Ticketing-Integration-Strategy.md) | ADO / JIRA / ServiceNow integration |

## Business / Management
| Document | Purpose |
|---|---|
| [business/Management-Presentation.md](business/Management-Presentation.md) | Management-facing pitch deck |

## Legacy / Superseded
| Document | Purpose |
|---|---|
| [legacy/README.md](legacy/README.md) | Index of superseded approaches, kept for historical reference only |

## Code
The implementation itself lives outside `docs/`, at the repository root:
- [`src/mcp-server/`](../src/mcp-server/) — Python MCP tool server (Azure Functions)
- [`src/Continuum.Ops.Functions/`](../src/Continuum.Ops.Functions/) — .NET 8 Durable Functions orchestrator + Repair Agent
- [`infrastructure/bicep/`](../infrastructure/bicep/) — Infrastructure as Code
- [`agents/`](../agents/) — Foundry Prompt Agent instructions and provisioning
