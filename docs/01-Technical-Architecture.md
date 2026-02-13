# Continuum-Ops: Technical Architecture
## Powered by Azure AI Agent Service

---

## Document Overview

This document provides the **complete technical architecture** for Continuum-Ops, an AI-native operational resilience platform built on Microsoft Azure AI Foundry.

**Related Documentation:**
- **[00-Product-Overview.md](00-Product-Overview.md)** - Product vision, internal value proposition
- **[02-Deployment-Guide.md](02-Deployment-Guide.md)** - 30-minute deployment playbook
- **[03-User-Manual.md](03-User-Manual.md)** - Operations guide for daily use
- **[10-Implementation-Roadmap.md](10-Implementation-Roadmap.md)** - Development sprint plan

---

## Architecture Principles

### 1. AI-First, Using Native Azure Capabilities

```mermaid
flowchart LR
    subgraph Traditional[Traditional Monitoring]
        RULES[Rule Engine]
        SCRIPTS[Runbook Scripts]
        MANUAL[Manual Investigation]
    end
    
    subgraph ContinuumOps[Continuum-Ops]
        AGENT_SVC[Azure AI <br/>Agent Service]
        AZ_MON[Azure Monitor<br/>Dynamic Alerts]
        FUNCTIONS[Azure Functions<br/>Tooling]
    end
    
    style ContinuumOps fill:#50e6ff,stroke:#0078d4,stroke-width:8px
```

**Core Principle**: Every component is AI-native from the ground up, not rules with AI added on top.

### 2. Zero-Configuration Onboarding

**Vision**: App Team deploys → System auto-discovers → AI configures → Production ready in 30 minutes.

```mermaid
sequenceDiagram
    participant APP_TEAM as App Team
    participant DEPLOY as ARM Template
    participant DISCOVERY as Discovery Agent
    participant AI as AI Config Engine
    participant VALIDATE as Validation
    
    APP_TEAM->>DEPLOY: Click "Deploy to Azure"
    DEPLOY->>DEPLOY: Provision infrastructure (5 min)
    DEPLOY->>DISCOVERY: Grant RBAC permissions
    
    DISCOVERY->>DISCOVERY: Scan subscriptions for Service Bus
    DISCOVERY->>DISCOVERY: Find 47 integrations with tags
    
    DISCOVERY->>AI: Send integration metadata
    AI->>AI: Analyze patterns, traffic, history
    AI->>AI: Generate optimal policies per integration
    
    AI->>APP_TEAM: Review recommended config
    APP_TEAM->>VALIDATE: Approve policies
    VALIDATE->>VALIDATE: Test with synthetic failure
    
    VALIDATE->>APP_TEAM: ✅ Live in production (30 min total)
```

### 3. Business Outcome Focused

Traditional systems verify **technical success** (message processed).  
Continuum-Ops verifies **business outcomes** (order completed in ERP).

```mermaid
flowchart TD
    REPAIR[Repair Action Executed]
    
    subgraph Traditional[Traditional Verification]
        T1[Message left DLQ?]
        T2[No errors in logs?]
        T3[✅ Done]
    end
    
    subgraph ContinuumOps[Continuum-Ops Verification]
        C1[Message processed?]
        C2[Business entity created in ERP?]
        C3[Downstream processes triggered?]
        C4[No duplicate side effects?]
        C5[Customer experience impact?]
        C6[✅ Business outcome verified]
    end
    
    REPAIR --> Traditional
    REPAIR --> ContinuumOps
    
    T1 --> T2 --> T3
    C1 --> C2 --> C3 --> C4 --> C5 --> C6
    
    style C6 fill:#90EE90,stroke:#006400,stroke-width:3px
```

---

## Sequence diagram (example)

Scenario: **Order message fails → agent diagnoses missing customer → creates customer → replays message → order succeeds → Teams notification**

```mermaid
sequenceDiagram
  autonumber
  participant SB as Service Bus (Topic/Sub + DLQ)
  participant W as Watcher Agent (Functions)
  participant ORCH as Durable Orchestrator
  participant DIAG as Diagnosis Agent
  participant AOAI as Azure OpenAI
  participant COS as Cosmos DB
  participant REP as Repair Agent
  participant ERP as ERP / Master Data API
  participant VER as Verification Agent
  participant TEAMS as Microsoft Teams

  SB-->>W: DLQ spike / failure signal (order message dead-lettered)
  W->>ORCH: StartIncident(correlationId, entity, messageId)

  ORCH->>DIAG: CollectEvidence(message peek, headers, traces)
  DIAG->>SB: Peek DLQ message (read-only)
  DIAG->>COS: Load past incidents/patterns
  DIAG->>AOAI: Diagnose + propose repair plan (bounded schema)
  AOAI-->>DIAG: MissingCustomer + plan: CreateCustomer -> ReplayMessage + confidence=0.86
  DIAG-->>ORCH: DiagnosisResult + Plan + Confidence

  ORCH->>COS: Write Incident state (Diagnosis)
  ORCH->>ORCH: PolicyGate(confidence>=0.80, action allowed?)

  alt Auto-approved
    ORCH->>REP: ExecutePlan(incidentId, plan)
    REP->>ERP: CreateCustomer(customerId, payload subset)
    ERP-->>REP: 201 Created
    REP->>SB: Replay message (DLQ -> active / resubmit)
    SB-->>REP: Accepted

    ORCH->>VER: VerifyOutcome(correlationId)
    VER->>SB: Check message consumed + DLQ stable
    VER->>ERP: Confirm order processed (optional)
    VER-->>ORCH: Verified

    ORCH->>COS: Close incident + store actions
    ORCH->>TEAMS: Notify success + summary + audit link
  else Needs approval
    ORCH->>TEAMS: Request approval with plan + risk
    TEAMS-->>ORCH: Approve/Reject
  end
```

---

## Azure AI Agent Service Design

### Agent Topology

We utilize the **Azure AI Agent Service** to host a "Coordinator" agent that manages specialized worker agents. This replaces custom orchestrators with a managed, scalable runtime.

```mermaid
graph TB
    subgraph Detection[Detection Layer]
        AZMON[Azure Monitor<br/>Dynamic Thresholds<br/>Replaces Custom Watcher]
    end

    subgraph AgentService[Azure AI Agent Service]
        COORD["Coordinator Agent<br/>(Router & State Manager)"]
        
        subgraph Workers[Specialized Agents]
            DIAG["Diagnostician<br/>(RCA & Reasoning)"]
            PLAN["Planner<br/>(Safety & Sequencing)"]
            EXEC["Executor<br/>(Tool Invocation)"]
            LEARN["Learner<br/>(Pattern Updates)"]
        end
    end

    subgraph Skills[Tooling / Skills]
        OPENAPI["OpenAPI Definitions<br/>Azure Functions"]
        SEARCH["Vector Search<br/>Azure AI Search"]
    end
    
    AZMON -->|Alert Webhook| COORD
    COORD -->|Delegates| DIAG
    DIAG -->|Queries| SEARCH
    DIAG -->|Output| PLAN
    PLAN -->|Plan| COORD
    COORD -->|Approves| EXEC
    EXEC -->|Calls| OPENAPI
    EXEC -->|Result| LEARN
    LEARN -->|Updates| SEARCH
    
    style AZMON fill:#FFD700,stroke:#FF8C00,stroke-width:2px
    style AgentService fill:#50e6ff,stroke:#0078d4,stroke-width:3px
```

### Component Specifications

#### 1. Detection: Azure Monitor (Dynamic Thresholds)
*Instead of a custom "Watcher Agent", we leverage Azure's native ML capabilities.*
*   **Feature**: Azure Monitor Metric Alerts with Dynamic Thresholds.
*   **Metric**: `DeadletterMessageCount`, `ActiveMessageCount`.
*   **Configuration**: High Sensitivity.
*   **Action**: Calls Continuum-Ops Webhook (starts investigation workflow).
*   **Benefit**: Zero code to maintain, built-in seasonality learning.

#### 2. Coordinator Agent (Azure AI Agent Service)
*   **Role**: The entry point for all incidents.
*   **Responsibility**: Maintains conversation state, routes tasks to sub-agents, and handles "Human-in-the-loop" transitions.
*   **Implementation**: Azure AI Agent configured with `Router` instructions.

#### 3. Diagnostician Agent
*   **Role**: Root Cause Analysis.
*   **Tools**: 
    *   `query_logs` (App Insights via KQL)
    *   `search_patterns` (Azure AI Search)
*   **Model**: GPT-4 Turbo (or GPT-4o for complex reasoning).

#### 4. Executor Agent & Standardized Tooling
*Instead of proprietary MCP servers, we use standard Azure Functions with OpenAPI definitions.*
*   **Decision**: We deliberately chose **OpenAPI** over the emerging Model Context Protocol (MCP) for the discovery layer.
*   **Rationale**:
    *   **Maturity**: OpenAPI (Swagger) is the industry standard for REST APIs, supported natively by Azure Functions, Logic Apps, and Power Platform.
    *   **Ecosystem**: Every Azure service emits OpenAPI definitions; MCP is still experimental and lacks native Azure integration.
    *   **Security**: OpenAPI integrates directly with Azure AD (Entra ID) authentication flows, whereas MCP requires custom transport security.
*   **Implementation**: Azure Functions (.NET 8) exposing Swagger/OpenAPI.
*   **Integration**: Azure AI Agents ingest the OpenAPI spec to understand available tools.

**Tool Interface (OpenAPI):**
```yaml
paths:
  /servicebus/replay:
    post:
      operationId: ReplayMessages
      summary: Replays messages from DLQ back to active queue
      parameters:
        - name: namespace
          in: query
          required: true
          type: string
        - name: queue
          in: query
          required: true
          type: string
        - name: count
          in: query
          type: integer
```

---

## Data Architecture

### Cosmos DB Container Design

```mermaid
erDiagram
    INCIDENTS ||--o{ EVIDENCE : contains
    INCIDENTS ||--o{ ACTIONS : executes
    INCIDENTS ||--o{ AUDIT_EVENTS : tracks
    INCIDENTS }o--|| PATTERNS : matches
    INTEGRATIONS ||--o{ INCIDENTS : generates
    INTEGRATIONS ||--|| POLICIES : enforces
    INTEGRATIONS ||--o{ RUNBOOKS : uses
    
    INCIDENTS {
        string id PK
        string tenantId
        string integrationId
        string status
        datetime detected_at
        datetime resolved_at
        string orchestrationInstanceId
        object diagnosis
        float confidence
        string rca_id
        array agent_interactions
    }
    
    EVIDENCE {
        string id PK
        string incidentId FK
        string collected_by_agent
        string evidence_type
        datetime collected_at
        object data
        bool pii_redacted
        int ttl
    }
    
    ACTIONS {
        string id PK
        string incidentId FK
        string executed_by_agent
        string action_name
        object parameters
        string status
        datetime executed_at
        object result
        bool idempotent_check_passed
    }
    
    PATTERNS {
        string id PK
        string signature_hash
        string integrationId
        string root_cause_category
        array typical_actions
        float success_rate
        int occurrence_count
        datetime last_seen
        object vector_embedding
    }
    
    INTEGRATIONS {
        string id PK
        string integrationId
        string tenantId
        string environment
        object servicebus_config
        bool autoheal_enabled
        datetime discovered_at
        object ai_recommended_policy
    }
    
    POLICIES {
        string id PK
        string integrationId
        float confidence_threshold
        array allowed_actions
        object rate_limits
        object circuit_breaker_config
        int version
    }
    
    RUNBOOKS {
        string id PK
        string action_name
        string risk_level
        bool approval_required
        object parameters_schema
        array verification_criteria
        string executor_function
    }
    
    AUDIT_EVENTS {
        string id PK
        string incidentId FK
        datetime timestamp
        string event_type
        string actor_agent
        object details
        bool immutable
    }
```

**Partition Keys**:
- `Incidents`: `/integrationId` (co-locates all incidents for same integration)
- `Evidence`: `/incidentId` (co-locates evidence with parent incident)
- `Patterns`: `/integrationId` (efficient pattern lookup per integration)
- `Integrations`: `/tenantId` (multi-tenant isolation)

### AI Search Index (Semantic Memory)

```json
{
  "name": "incident-patterns",
  "fields": [
    {"name": "id", "type": "Edm.String", "key": true},
    {"name": "signature_hash", "type": "Edm.String", "filterable": true},
    {"name": "root_cause_text", "type": "Edm.String", "searchable": true},
    {"name": "resolution_text", "type": "Edm.String", "searchable": true},
    {"name": "evidence_summary", "type": "Edm.String", "searchable": true},
    {"name": "embedding", "type": "Collection(Edm.Single)", "vectorSearch": true, "dimensions": 1536},
    {"name": "success_rate", "type": "Edm.Double", "filterable": true, "sortable": true},
    {"name": "occurrence_count", "type": "Edm.Int32", "sortable": true},
    {"name": "integration_id", "type": "Edm.String", "filterable": true}
  ],
  "vectorSearch": {
    "algorithms": [
      {
        "name": "hnsw-config",
        "kind": "hnsw",
        "hnswParameters": {
          "m": 4,
          "efConstruction": 400,
          "efSearch": 500,
          "metric": "cosine"
        }
      }
    ]
  }
}
```

**Semantic Search Query** (from Diagnostician Agent):
```csharp
var searchClient = new SearchClient(endpoint, "incident-patterns", credential);

var searchOptions = new SearchOptions
{
    VectorSearch = new()
    {
        Queries = { new VectorizedQuery(evidenceEmbedding) { KNearestNeighborsCount = 5 } }
    },
    Filter = $"integration_id eq '{integrationId}' and success_rate gt 0.7",
    OrderBy = { "occurrence_count desc" }
};

var results = await searchClient.SearchAsync<IncidentPattern>(null, searchOptions);
```

---

## Infrastructure Architecture

### Multi-Tenant Deployment

```mermaid
C4Deployment
    title Production Deployment Architecture

    Deployment_Node(azure, "Azure Cloud", "East US Region") {
        Deployment_Node(func_plan, "Azure Functions Premium Plan", "EP2, 2 instances") {
            Container(agents, "AI Agent Functions", ".NET 8", "All agent implementations")
            Container(orchestrator, "Durable Functions", ".NET 8", "Incident orchestration")
        }
        
        Deployment_Node(data, "Data Layer") {
            ContainerDb(cosmos, "Cosmos DB", "NoSQL", "Incidents, patterns, audit")
            ContainerDb(aisearch, "AI Search", "Cognitive Search", "Semantic memory, vectors")
        }
        
        Deployment_Node(ai, "AI Services") {
            Container(openai, "Azure OpenAI", "GPT-4 Turbo", "Reasoning engine")
            Container(aifoundry, "AI Foundry", "Agent orchestration", "Multi-agent framework")
        }
        
        Deployment_Node(gateway, "API Gateway") {
            Container(apim, "API Management", "Developer tier", "Internal API + rate limiting")
        }
    }
    
    Deployment_Node(customer_env, "Business Unit Tenant", "Cross-subscription") {
        System_Ext(sb, "Service Bus", "Business Unit namespaces")
        System_Ext(erp, "ERP", "Dynamics 365, SAP")
    }
    
    Rel(agents, cosmos, "Read/Write", "Managed Identity")
    Rel(agents, aisearch, "Semantic search", "Managed Identity")
    Rel(agents, openai, "AI calls", "Managed Identity")
    Rel(agents, sb, "Monitor/heal", "Cross-sub Managed Identity")
    Rel(orchestrator, erp, "Remediate", "OAuth 2.0")
```

---

## Security Architecture

### Zero-Trust Model

```mermaid
flowchart TB
    subgraph Identity[Identity Layer]
        MI[System-Assigned<br/>Managed Identity]
        ENTRA[Microsoft Entra ID<br/>Authentication]
    end
    
    subgraph DataPlane[Data Plane Access]
        COSMOS_RBAC[Cosmos DB<br/>Built-in RBAC]
        SB_RBAC[Service Bus<br/>Data Receiver/Sender]
        OPENAI_RBAC[Azure OpenAI<br/>Cognitive Services User]
    end
    
    subgraph Network[Network Security]
        PE[Private Endpoints<br/>Optional]
        APIM_IP[API Management<br/>IP restrictions]
        VNET[VNET Integration<br/>Function App]
    end
    
    subgraph Secrets[Secrets Management]
        KV[Azure Key Vault<br/>Secrets storage]
        KV_REF[Key Vault References<br/>App Settings]
    end
    
    subgraph Audit[Audit & Compliance]
        IMMUTABLE[Immutable Audit Log<br/>Cosmos DB]
        EXPORT[Blob Storage<br/>Long-term retention]
        SENTINEL[Microsoft Sentinel<br/>Security monitoring]
    end
    
    MI --> ENTRA
    ENTRA --> DataPlane
    MI --> KV
    KV --> KV_REF
    
    DataPlane --> COSMOS_RBAC
    DataPlane --> SB_RBAC
    DataPlane --> OPENAI_RBAC
    
    COSMOS_RBAC --> IMMUTABLE
    IMMUTABLE --> EXPORT
    EXPORT --> SENTINEL
    
    style MI fill:#FFD700,stroke:#FF8C00,stroke-width:3px
    style IMMUTABLE fill:#FFEFD5,stroke:#FF8C00,stroke-width:2px
```

---

## Deployment Models

### Deployment Architecture

```mermaid
flowchart TB
    subgraph ProjectSub[Project Subscription]
        DEDICATED[Continuum-Ops Instance]
        C_SB[Service Bus]
        C_ERP[ERP]
    end
    
    DEDICATED -->|Same subscription| C_SB
    DEDICATED -->|Same subscription| C_ERP
    
    style DEDICATED fill:#90EE90,stroke:#006400,stroke-width:3px
```

The platform is designed to be deployed directly into the project's Azure subscription, ensuring complete data isolation and security.

---

## Scalability & Performance

### Scaling Dimensions

| Component | Scaling Strategy | Target Capacity |
|-----------|------------------|-----------------|
| **AI Agents** | Azure Functions auto-scale (EP2 plan) | 100+ concurrent incidents |
| **Durable Functions** | Partition count = 16 | 1000+ orchestrations/min |
| **Cosmos DB** | Autoscale 4000-40000 RU/s | 10K writes/sec |
| **AI Search** | Standard tier, 3 replicas | 50 queries/sec |
| **Azure OpenAI** | 100K TPM quota | 500 diagnoses/hour |

**Performance Targets**:
- Detection latency: <2 min (P95)
- Diagnosis latency: <30 sec (P95)
- Repair latency: <60 sec (P95)
- End-to-end MTTR: <10 min (P95)

---

## Technology Stack Summary

| Category | Technology | Version | Purpose |
|----------|-----------|---------|---------|
| **AI Orchestration** | Azure AI Foundry | Preview | Multi-agent system |
| **LLM** | Azure OpenAI GPT-4 Turbo | 1106-preview | Reasoning, diagnosis |
| **LLM (Advanced)** | Azure OpenAI GPT-4o | Latest | Chain-of-thought reasoning |
| **AI Framework** | Semantic Kernel | 1.x | Agent development |
| **LLM Workflows** | Prompt Flow | 1.x | Visual LLM pipelines |
| **Orchestration** | Durable Functions | 2.x | Stateful workflows |
| **Runtime** | .NET | 8.0 | Function App runtime |
| **Database** | Cosmos DB | Core SQL API | Incidents, patterns, audit |
| **Vector DB** | AI Search | Standard | Semantic memory |
| **Observability** | Application Insights | Latest | Telemetry, monitoring |
| **Identity** | Managed Identity | System-assigned | Zero-trust auth |
| **Collaboration** | Microsoft Teams | Latest | Approval workflows |
| **IaC** | Bicep | 0.24+ | Infrastructure provisioning |

---

## References

- **[00-Product-Overview.md](00-Product-Overview.md)** - Product vision
- **[02-Deployment-Guide.md](02-Deployment-Guide.md)** - Deployment playbook
- **[03-User-Manual.md](03-User-Manual.md)** - Operations guide
- **[10-Implementation-Roadmap.md](10-Implementation-Roadmap.md)** - Development plan

---
`````
