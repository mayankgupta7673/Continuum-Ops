# Continuum-Ops: Technical Architecture
## AI Foundry Multi-Agent System Design

---

## Document Overview

This document provides the **complete technical architecture** for Continuum-Ops, an AI-native operational resilience platform built on Microsoft Azure AI Foundry.

**Related Documentation:**
- **[00-Product-Overview.md](00-Product-Overview.md)** - Product vision, market positioning, pricing
- **[02-Deployment-Guide.md](02-Deployment-Guide.md)** - 30-minute deployment playbook
- **[03-User-Manual.md](03-User-Manual.md)** - Operations guide for daily use
- **[10-Implementation-Roadmap.md](10-Implementation-Roadmap.md)** - Development sprint plan

---

## Architecture Principles

### 1. AI-First, Not AI-Retrofitted

```mermaid
flowchart LR
    subgraph Traditional[Traditional Monitoring]
        RULES[Rule Engine]
        SCRIPTS[Runbook Scripts]
        MANUAL[Manual Investigation]
    end
    
    subgraph ContinuumOps[Continuum-Ops]
        AIFOUNDRY[Azure AI Foundry<br/>Multi-Agent System]
        REASONING[GPT-4 Turbo<br/>Reasoning Engine]
        LEARNING[Semantic Memory<br/>Pattern Learning]
    end
    
    Traditional -->|Limited to known patterns| RULES
    Traditional -->|Static procedures| SCRIPTS
    Traditional -->|Human expertise required| MANUAL
    
    ContinuumOps -->|Handles novel failures| AIFOUNDRY
    ContinuumOps -->|Dynamic adaptation| REASONING
    ContinuumOps -->|Continuous improvement| LEARNING
    
    style ContinuumOps fill:#50e6ff,stroke:#0078d4,stroke-width:3px
```

**Core Principle**: Every component is AI-native from the ground up, not rules with AI added on top.

### 2. Zero-Configuration Onboarding

**Vision**: Customer deploys → System auto-discovers → AI configures → Production ready in 30 minutes.

```mermaid
sequenceDiagram
    participant CUSTOMER as Customer
    participant DEPLOY as ARM Template
    participant DISCOVERY as Discovery Agent
    participant AI as AI Config Engine
    participant VALIDATE as Validation
    
    CUSTOMER->>DEPLOY: Click "Deploy to Azure"
    DEPLOY->>DEPLOY: Provision infrastructure (5 min)
    DEPLOY->>DISCOVERY: Grant RBAC permissions
    
    DISCOVERY->>DISCOVERY: Scan subscriptions for Service Bus
    DISCOVERY->>DISCOVERY: Find 47 integrations with tags
    
    DISCOVERY->>AI: Send integration metadata
    AI->>AI: Analyze patterns, traffic, history
    AI->>AI: Generate optimal policies per integration
    
    AI->>CUSTOMER: Review recommended config
    CUSTOMER->>VALIDATE: Approve policies
    VALIDATE->>VALIDATE: Test with synthetic failure
    
    VALIDATE->>CUSTOMER: ✅ Live in production (30 min total)
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

## System Architecture

### High-Level Architecture (Azure AI Foundry)

```mermaid
C4Container
    title Continuum-Ops Platform Architecture

    Person(ops, "Operations Team", "Approves high-risk actions")
    
    System_Boundary(platform, "Continuum-Ops Platform") {
        Container(aifoundry, "AI Foundry Agent System", "Multi-agent orchestration", "Supervisor + 7 specialized agents")
        Container(memory, "Semantic Memory", "AI Search + Cosmos DB", "Pattern storage, agent memory")
        Container(orchestrator, "Incident Orchestrator", "Durable Functions", "Stateful workflow engine")
        Container(api, "Management API", "API Management", "Customer configuration & monitoring")
    }
    
    System_Ext(customer_sb, "Customer Service Bus", "Message queues & topics")
    System_Ext(customer_erp, "Customer ERP", "Dynamics 365, SAP, etc.")
    System_Ext(observability, "Azure Monitor", "App Insights, Log Analytics")
    System_Ext(teams, "Microsoft Teams", "Approval workflows")
    
    Rel(customer_sb, aifoundry, "Failure signals", "Metrics, DLQ events")
    Rel(observability, aifoundry, "Telemetry", "Logs, traces, exceptions")
    Rel(aifoundry, memory, "Stores/retrieves", "Patterns, context")
    Rel(aifoundry, orchestrator, "Triggers", "Incident workflows")
    Rel(orchestrator, customer_sb, "Remediates", "Replay, isolate")
    Rel(orchestrator, customer_erp, "Fixes data", "Create entities")
    Rel(orchestrator, teams, "Requests approval", "Adaptive cards")
    Rel(ops, teams, "Approves/rejects")
    Rel(ops, api, "Configures", "Policies, integrations")
```

---

## Azure AI Foundry Multi-Agent System

### Agent Topology

```mermaid
graph TB
    subgraph Supervisor[Supervisor Agent - Central Coordinator]
        SUPER[Supervisor<br/>Orchestrates sub-agents<br/>Manages state transitions]
    end
    
    subgraph PerceptionAgents[Perception Layer]
        WATCHER[Watcher Agent<br/>Anomaly detection<br/>Signal correlation]
        ANALYZER[Analyzer Agent<br/>Evidence collection<br/>Cross-system correlation]
    end
    
    subgraph ReasoningAgents[Reasoning Layer]
        DIAGNOSTICIAN[Diagnostician Agent<br/>GPT-4 Turbo RCA<br/>Confidence scoring]
        PLANNER[Planner Agent<br/>Action sequencing<br/>Risk assessment]
    end
    
    subgraph ExecutionAgents[Execution Layer]
        EXECUTOR[Executor Agent<br/>Safe remediation<br/>Idempotency checks]
        VERIFIER[Verifier Agent<br/>Outcome validation<br/>Business impact]
    end
    
    subgraph LearningAgent[Learning Layer]
        LEARNER[Learner Agent<br/>Pattern extraction<br/>Model calibration]
    end
    
    subgraph SharedInfra[Shared Infrastructure]
        MEMORY[Semantic Memory<br/>AI Search + Cosmos DB]
        TOOLS[Tool Registry<br/>Service Bus, ERP, etc.]
        POLICY[Policy Engine<br/>Guardrails & approval]
    end
    
    SUPER -->|Delegate| PerceptionAgents
    PerceptionAgents -->|Evidence| ReasoningAgents
    ReasoningAgents -->|Plan| POLICY
    POLICY -->|Approved| ExecutionAgents
    ExecutionAgents -->|Results| LearningAgent
    LearningAgent -->|Updates| MEMORY
    
    MEMORY <-->|Context| SUPER
    MEMORY <-->|Patterns| DIAGNOSTICIAN
    TOOLS <-->|Actions| EXECUTOR
    
    style SUPER fill:#0078d4,stroke:#004578,stroke-width:4px,color:#fff
    style DIAGNOSTICIAN fill:#50e6ff,stroke:#0078d4,stroke-width:3px
    style POLICY fill:#FFD700,stroke:#FF8C00,stroke-width:2px
```

### Agent Specifications

#### Supervisor Agent
**Technology**: Azure AI Foundry Orchestrator + Semantic Kernel

**Responsibilities**:
- Central coordination of all sub-agents
- Incident state management (Detected → Closed)
- Sub-agent task assignment and monitoring
- Escalation decision-making
- Audit trail generation

**AI Capabilities**:
- ✨ **Dynamic task planning**: Adapts workflow based on incident complexity
- ✨ **Agent selection**: Chooses which sub-agents to invoke
- ✨ **Conflict resolution**: Handles disagreements between agents
- ✨ **Timeout management**: Escalates stuck incidents

---

#### Watcher Agent (Perception)
**Technology**: Azure Functions + Azure Monitor AI + GPT-4 Turbo

**Responsibilities**:
- Monitor Service Bus metrics (DLQ depth, active count, age)
- Detect anomalies in message flow patterns
- Correlate Service Bus events with Application Insights exceptions
- Generate incident triggers with initial context

**AI Capabilities**:
- ✨ **Anomaly detection**: Learns normal behavior, flags deviations
- ✨ **Pattern recognition**: Identifies recurring failure signatures
- ✨ **Semantic correlation**: Links messages, logs, and metrics by meaning
- ✨ **Adaptive thresholds**: Adjusts sensitivity based on time-of-day, load

**Implementation**:
```csharp
// Watcher Agent - Anomaly Detection with AI
public class WatcherAgent : IAgent
{
    private readonly IAIFoundryClient _aiFoundry;
    private readonly ISemanticMemory _memory;
    
    public async Task<IncidentTrigger?> DetectAnomalyAsync(ServiceBusMetrics metrics)
    {
        // 1. Check if metrics are anomalous (AI-powered)
        var baseline = await _memory.GetBaselineBehaviorAsync(metrics.IntegrationId);
        var anomalyScore = await _aiFoundry.DetectAnomalyAsync(metrics, baseline);
        
        if (anomalyScore < 0.7) return null; // Not anomalous
        
        // 2. Correlate with other signals
        var exceptions = await _observability.GetRecentExceptionsAsync(metrics.CorrelationId);
        var similarIncidents = await _memory.FindSimilarIncidentsAsync(metrics.Signature);
        
        // 3. Generate incident trigger
        return new IncidentTrigger
        {
            IntegrationId = metrics.IntegrationId,
            DetectedAt = DateTime.UtcNow,
            AnomalyScore = anomalyScore,
            InitialEvidence = new { metrics, exceptions, similarIncidents }
        };
    }
}
```

---

#### Analyzer Agent (Perception)
**Technology**: Semantic Kernel + Application Insights SDK + Log Analytics SDK

**Responsibilities**:
- Collect detailed evidence (DLQ messages, logs, metrics, traces)
- Cross-reference data across multiple Azure services
- PII detection and auto-redaction
- Build structured evidence bundle for Diagnostician

**AI Capabilities**:
- ✨ **Intelligent log parsing**: Extracts key entities (IDs, timestamps, errors)
- ✨ **Temporal reasoning**: Understands event sequence causality
- ✨ **PII detection**: Auto-identifies and redacts sensitive data
- ✨ **Context enrichment**: Adds integration metadata, historical patterns

**Evidence Bundle Schema**:
```json
{
  "incident_id": "inc-2026-02-13-001",
  "collected_at": "2026-02-13T10:45:00Z",
  "evidence": {
    "service_bus": {
      "dlq_messages": [
        {
          "message_id": "msg-12345",
          "body": "<redacted>",
          "headers": {"correlationId": "ORD-67890"},
          "dead_letter_reason": "Customer not found",
          "enqueued_at": "2026-02-13T10:30:00Z"
        }
      ],
      "metrics": {
        "dlq_depth": 47,
        "active_count": 120,
        "avg_processing_time_ms": 250
      }
    },
    "application_insights": {
      "exceptions": [
        {
          "timestamp": "2026-02-13T10:30:15Z",
          "type": "CustomerNotFoundException",
          "message": "Customer CUS-12345 not found in ERP",
          "operation_name": "ProcessOrder",
          "dependency": "erp-api"
        }
      ],
      "traces": [/* correlated traces */]
    },
    "historical_context": {
      "similar_incidents": 5,
      "typical_resolution": "create_customer",
      "success_rate": 0.95
    }
  },
  "pii_redacted": true,
  "redaction_summary": ["customer_name", "email", "phone"]
}
```

---

#### Diagnostician Agent (Reasoning)
**Technology**: GPT-4 Turbo + Prompt Flow + Semantic Kernel

**Responsibilities**:
- Root cause analysis from evidence bundle
- Confidence scoring (calibrated against historical accuracy)
- Proposed action plan with sequencing
- Evidence citations for explainability

**AI Capabilities**:
- ✨ **Multi-modal reasoning**: Analyzes text logs + metrics + message payloads
- ✨ **Chain-of-thought**: Explains reasoning process step-by-step
- ✨ **Pattern matching**: Compares to historical incidents (semantic similarity)
- ✨ **Confidence calibration**: Adjusts based on past prediction accuracy

**Prompt Flow Implementation**:
```yaml
name: DiagnosisWorkflow
display_name: Incident Diagnosis with GPT-4 Turbo

inputs:
  evidence_bundle:
    type: object
  integration_metadata:
    type: object

outputs:
  diagnosis:
    type: object

nodes:
  # Node 1: Pattern Matching (Semantic Search)
  - name: find_similar_patterns
    type: python
    source:
      type: code
      path: semantic_search.py
    inputs:
      query: ${inputs.evidence_bundle.signature}
      index: historical_incidents
      top_k: 5
    
  # Node 2: Evidence Analysis (GPT-4 Turbo)
  - name: analyze_evidence
    type: llm
    source:
      type: code
      path: prompts/analyze_evidence.jinja2
    inputs:
      deployment_name: gpt-4-turbo
      temperature: 0.1
      max_tokens: 2000
      evidence: ${inputs.evidence_bundle}
      similar_patterns: ${find_similar_patterns.output}
    
  # Node 3: Root Cause Synthesis (GPT-4o with reasoning)
  - name: synthesize_root_cause
    type: llm
    source:
      type: code
      path: prompts/synthesize_rca.jinja2
    inputs:
      deployment_name: gpt-4o
      temperature: 0.0
      response_format: 
        type: json_schema
        json_schema:
          name: diagnosis_output
          schema:
            type: object
            properties:
              root_cause_hypothesis: {type: string}
              evidence_citations: {type: array}
              proposed_actions: {type: array}
              confidence_raw: {type: number}
              risk_level: {type: string, enum: [low, medium, high]}
      analysis: ${analyze_evidence.output}
      patterns: ${find_similar_patterns.output}
    
  # Node 4: Confidence Calibration (Python)
  - name: calibrate_confidence
    type: python
    source:
      type: code
      path: calibrate_confidence.py
    inputs:
      diagnosis: ${synthesize_root_cause.output}
      integration_id: ${inputs.integration_metadata.integrationId}
      historical_accuracy: ${find_similar_patterns.output.success_rate}
    outputs:
      - calibrated_diagnosis
```

**Sample Diagnosis Output**:
```json
{
  "diagnosis_id": "diag-2026-02-13-001",
  "incident_id": "inc-2026-02-13-001",
  "root_cause_hypothesis": "Order processing failed because customer CUS-12345 does not exist in the ERP system. The customer sync job from CRM failed 2 hours prior, leaving a data gap.",
  "evidence_citations": [
    "Service Bus DLQ message: 'Customer not found' (dead_letter_reason)",
    "Application Insights exception: CustomerNotFoundException at 10:30:15 UTC",
    "Log Analytics trace: ERP API returned HTTP 404 for GET /customers/CUS-12345",
    "Historical pattern match: 5 similar incidents resolved by creating customer (95% success)"
  ],
  "proposed_actions": [
    {
      "sequence": 1,
      "action": "create_customer",
      "parameters": {
        "customer_id": "CUS-12345",
        "source": "crm_sync_backfill",
        "minimal_profile": true
      },
      "estimated_duration_seconds": 30,
      "idempotency_check": "GET /customers/CUS-12345 returns 404"
    },
    {
      "sequence": 2,
      "action": "replay_messages",
      "parameters": {
        "queue_path": "orders-queue/$DeadLetterQueue",
        "filter": "correlationId = 'ORD-67890'",
        "batch_size": 3
      },
      "estimated_duration_seconds": 60,
      "depends_on": ["create_customer"]
    }
  ],
  "confidence": 0.92,
  "confidence_raw": 0.88,
  "confidence_calibration": {
    "reason": "Boosted by 0.04 due to 95% historical success rate for this pattern",
    "historical_accuracy": 0.95
  },
  "risk_level": "medium",
  "risk_factors": [
    "Creating master data in production ERP (medium risk)",
    "Replaying 3 messages (low risk)",
    "High confidence in diagnosis (reduces risk)"
  ],
  "reasoning_trace": [
    "Step 1: Identified 'Customer not found' as primary error from DLQ message",
    "Step 2: Correlated with HTTP 404 from ERP API call to /customers/CUS-12345",
    "Step 3: Found 5 historical incidents with identical signature",
    "Step 4: Verified customer sync job failed 2 hours prior (root cause)",
    "Step 5: Proposed creating customer + replaying messages (proven pattern)"
  ]
}
```

---

#### Planner Agent (Reasoning)
**Technology**: Semantic Kernel + GPT-4 Turbo

**Responsibilities**:
- Validate proposed actions against runbook catalog
- Sequence actions with dependency awareness
- Calculate blast radius and rollback requirements
- Generate compensation plan for failures

**AI Capabilities**:
- ✨ **Dependency resolution**: Orders actions correctly (create before replay)
- ✨ **Risk quantification**: Calculates blast radius per action
- ✨ **Rollback planning**: Defines undo steps for each action
- ✨ **Optimization**: Parallelizes independent actions

---

#### Executor Agent (Execution)
**Technology**: Azure Functions + Semantic Kernel + Polly (resilience)

**Responsibilities**:
- Execute approved action plan
- Idempotency validation before each action
- Retry with exponential backoff for transient failures
- Emit detailed execution logs

**AI Capabilities**:
- ✨ **Idempotency validation**: Checks if action already executed
- ✨ **Adaptive retry**: Learns optimal backoff strategy per integration
- ✨ **Graceful degradation**: Handles partial success scenarios

**Implementation**:
```csharp
public class ExecutorAgent : IAgent
{
    private readonly IActionCatalog _catalog;
    private readonly IIdempotencyService _idempotency;
    
    public async Task<ExecutionResult> ExecuteActionPlanAsync(
        ActionPlan plan, 
        ApprovalContext approval)
    {
        var results = new List<ActionResult>();
        
        foreach (var action in plan.Actions.OrderBy(a => a.Sequence))
        {
            // 1. Idempotency check
            if (await _idempotency.WasAlreadyExecutedAsync(action.ActionId))
            {
                _logger.LogInformation("Action {ActionId} already executed, skipping", action.ActionId);
                results.Add(new ActionResult { Status = "Skipped", Reason = "Idempotent" });
                continue;
            }
            
            // 2. Get runbook executor
            var executor = await _catalog.GetExecutorAsync(action.Action);
            
            // 3. Execute with retry policy
            var retryPolicy = Policy
                .Handle<TransientException>()
                .WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
            
            try
            {
                var result = await retryPolicy.ExecuteAsync(async () =>
                    await executor.ExecuteAsync(action.Parameters));
                
                results.Add(result);
                
                // 4. Mark as executed (idempotency tracking)
                await _idempotency.MarkExecutedAsync(action.ActionId, result);
            }
            catch (Exception ex)
            {
                // 5. Compensation (rollback)
                await CompensateAsync(results, action);
                throw;
            }
        }
        
        return new ExecutionResult { Actions = results };
    }
}
```

---

#### Verifier Agent (Execution)
**Technology**: Azure Functions + GPT-4 Turbo

**Responsibilities**:
- Wait for async processing to complete (with timeout)
- Verify technical success (message consumed, DLQ cleared)
- Verify business outcome (entity state in ERP)
- Detect duplicate side effects

**AI Capabilities**:
- ✨ **Outcome prediction**: Predicts what should happen after repair
- ✨ **Multi-signal verification**: Checks technical + business + user impact
- ✨ **False positive detection**: Distinguishes correlation from causation

**Verification Workflow**:
```mermaid
sequenceDiagram
    participant EXEC as Executor Agent
    participant VER as Verifier Agent
    participant SB as Service Bus
    participant ERP as ERP System
    participant MEM as Semantic Memory
    
    EXEC->>VER: Repair completed
    VER->>VER: Wait 30s (async processing)
    
    Note over VER: Technical Verification
    VER->>SB: Get DLQ metrics
    SB-->>VER: DLQ depth: 44 (was 47)
    VER->>SB: Query for replayed message IDs
    SB-->>VER: Messages consumed
    
    Note over VER: Business Verification
    VER->>ERP: GET /customers/CUS-12345
    ERP-->>VER: 200 OK, customer exists
    VER->>ERP: GET /orders?correlationId=ORD-67890
    ERP-->>VER: 200 OK, 3 orders found
    
    Note over VER: Duplicate Detection
    VER->>ERP: GET /orders?customerId=CUS-12345
    ERP-->>VER: Count = 3 (no duplicates)
    
    Note over VER: Impact Assessment
    VER->>MEM: Store verification result
    VER->>EXEC: ✅ Verified (confidence: 0.95)
```

---

#### Learner Agent (Learning)
**Technology**: AI Search + Cosmos DB + GPT-4 Turbo

**Responsibilities**:
- Extract failure patterns from incidents
- Update pattern success rates
- Calibrate confidence models
- Generate preventive recommendations

**AI Capabilities**:
- ✨ **Pattern extraction**: Identifies recurring failure signatures
- ✨ **Confidence calibration**: Improves accuracy over time
- ✨ **Proactive recommendations**: Suggests preventive measures
- ✨ **Knowledge graph construction**: Builds integration dependency map

**Pattern Learning Flow**:
```mermaid
flowchart LR
    INCIDENT[Incident Closed]
    EXTRACT[Extract Pattern<br/>Signature + Resolution]
    EXISTING{Pattern<br/>Exists?}
    UPDATE[Update Stats<br/>Success rate, count]
    CREATE[Create New Pattern<br/>Initial confidence]
    CALIBRATE[Calibrate Confidence<br/>Adjust thresholds]
    RECOMMEND[Generate Preventive<br/>Recommendations]
    
    INCIDENT --> EXTRACT
    EXTRACT --> EXISTING
    EXISTING -->|Yes| UPDATE
    EXISTING -->|No| CREATE
    UPDATE --> CALIBRATE
    CREATE --> CALIBRATE
    CALIBRATE --> RECOMMEND
    
    style CALIBRATE fill:#90EE90,stroke:#006400,stroke-width:2px
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
            Container(apim, "API Management", "Developer tier", "Customer API + rate limiting")
        }
    }
    
    Deployment_Node(customer_env, "Customer Azure Tenant", "Cross-subscription") {
        System_Ext(sb, "Service Bus", "Customer namespaces")
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

### Model 1: SaaS (Recommended for Most Customers)

```mermaid
flowchart TB
    subgraph SaaS[Continuum-Ops SaaS Platform]
        SHARED[Shared AI Agent Pool<br/>All customers]
        ISOLATED[Tenant-Isolated Data<br/>Cosmos DB partitions]
    end
    
    subgraph Customer1[Customer A]
        C1_SB[Service Bus]
        C1_ERP[ERP]
    end
    
    subgraph Customer2[Customer B]
        C2_SB[Service Bus]
        C2_ERP[ERP]
    end
    
    SHARED -->|Managed Identity A| Customer1
    SHARED -->|Managed Identity B| Customer2
    SHARED <--> ISOLATED
    
    style SaaS fill:#50e6ff,stroke:#0078d4,stroke-width:3px
```

**Pros**: Fastest deployment, lowest cost, automatic updates  
**Cons**: Shared infrastructure (isolated data)

---

### Model 2: Private Deployment (Enterprise)

```mermaid
flowchart TB
    subgraph CustomerSub[Customer Subscription]
        DEDICATED[Dedicated Continuum-Ops<br/>Customer-owned infrastructure]
        C_SB[Service Bus]
        C_ERP[ERP]
    end
    
    DEDICATED -->|Same subscription| C_SB
    DEDICATED -->|Same subscription| C_ERP
    
    style DEDICATED fill:#90EE90,stroke:#006400,stroke-width:3px
```

**Pros**: Complete isolation, private network, customer control  
**Cons**: Higher cost, customer manages updates

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

- **[00-Product-Overview.md](00-Product-Overview.md)** - Product vision & business model
- **[02-Deployment-Guide.md](02-Deployment-Guide.md)** - Deployment playbook
- **[03-User-Manual.md](03-User-Manual.md)** - Operations guide
- **[10-Implementation-Roadmap.md](10-Implementation-Roadmap.md)** - Development plan

---

## Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 3.0 | 2026-02-13 | Architecture Team | Complete rewrite with AI Foundry multi-agent design |
| 2.0 | 2026-02-12 | Architecture Team | Added multi-customer deployment vision |
| 1.0 | 2026-01-15 | Architecture Team | Initial architecture document |
