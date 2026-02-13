# Continuum-Ops: API Reference
## REST APIs, SDKs, and Webhooks

---

## Overview

Continuum-Ops provides comprehensive APIs for:
- **Management API** - Configure policies, integrations, and settings
- **Query API** - Retrieve incidents, diagnoses, and audit logs
- **Webhook API** - Receive real-time notifications
- **SDKs** - .NET, TypeScript, Python client libraries

**Base URL**: `https://func-continuumops-{env}-{region}.azurewebsites.net/api`

**Authentication**: Azure AD OAuth 2.0 + Managed Identity

---

## Authentication

### Option 1: Managed Identity (Recommended)

```csharp
// .NET SDK
using Azure.Identity;
using ContinuumOps.Sdk;

var credential = new DefaultAzureCredential();
var client = new ContinuumOpsClient(
    new Uri("https://func-continuumops-prod-eastus.azurewebsites.net"),
    credential
);

var incidents = await client.Incidents.ListAsync(hours: 24);
```

### Option 2: Service Principal

```bash
# Get access token
ACCESS_TOKEN=$(az account get-access-token \
  --resource https://continuumops.azurewebsites.net \
  --query accessToken \
  --output tsv)

# Call API
curl -X GET "https://func-continuumops-prod-eastus.azurewebsites.net/api/incidents" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

### Option 3: Function Key (Dev/Test Only)

```bash
# Get function key
FUNCTION_KEY=$(az functionapp keys list \
  --name func-continuumops-prod-eastus \
  --resource-group rg-continuumops-prod-eastus \
  --query functionKeys.default \
  --output tsv)

# Call API
curl -X GET "https://func-continuumops-prod-eastus.azurewebsites.net/api/incidents?code=$FUNCTION_KEY"
```

---

## Management API

### Integrations

#### List Integrations
```http
GET /api/integrations
```

**Query Parameters:**
- `environment` (optional): Filter by environment (prod, staging, dev)
- `autoheal_enabled` (optional): Filter by autoheal status (true/false)

**Response:**
```json
{
  "integrations": [
    {
      "integration_id": "orders-to-erp",
      "namespace": "contoso-sb",
      "entity_path": "orders-queue",
      "entity_type": "queue",
      "autoheal_enabled": true,
      "environment": "production",
      "policy_id": "pol-001",
      "discovered_at": "2026-02-01T10:00:00Z",
      "last_incident_at": "2026-02-12T14:30:00Z",
      "incident_count_30d": 12,
      "auto_resolution_rate": 0.85
    }
  ],
  "total": 47
}
```

#### Get Integration Details
```http
GET /api/integrations/{integration_id}
```

**Response:**
```json
{
  "integration_id": "orders-to-erp",
  "namespace": "contoso-sb",
  "entity_path": "orders-queue",
  "entity_type": "queue",
  "subscription_id": "12345678-1234-1234-1234-123456789012",
  "resource_group": "rg-integrations",
  "autoheal_enabled": true,
  "policy": {
    "confidence_threshold": 0.85,
    "allowed_actions": [
      {"action": "replay_message", "approval_required": false},
      {"action": "create_customer", "approval_required": true}
    ],
    "circuit_breaker": {
      "failure_threshold": 5,
      "reset_timeout_minutes": 30
    }
  },
  "health": {
    "status": "healthy",
    "dlq_depth": 0,
    "active_messages": 120,
    "avg_processing_time_ms": 250,
    "last_check": "2026-02-13T00:15:00Z"
  },
  "tags": {
    "environment": "production",
    "owner": "ops-team",
    "cost_center": "1234"
  }
}
```

#### Register Integration (Manual)
```http
POST /api/integrations
Content-Type: application/json
```

**Request Body:**
```json
{
  "integration_id": "invoices-to-accounting",
  "namespace": "contoso-sb",
  "entity_path": "invoices-topic/subscriptions/accounting-sub",
  "entity_type": "subscription",
  "subscription_id": "12345678-1234-1234-1234-123456789012",
  "resource_group": "rg-integrations",
  "autoheal_enabled": true,
  "policy": {
    "confidence_threshold": 0.80,
    "allowed_actions": [
      {"action": "replay_message", "approval_required": false, "max_per_hour": 50}
    ]
  }
}
```

#### Update Integration
```http
PUT /api/integrations/{integration_id}
Content-Type: application/json
```

**Request Body:**
```json
{
  "autoheal_enabled": false,
  "policy": {
    "confidence_threshold": 0.90
  }
}
```

#### Delete Integration
```http
DELETE /api/integrations/{integration_id}
```

---

### Policies

#### Get Policy
```http
GET /api/policies/{integration_id}
```

**Response:**
```json
{
  "integration_id": "orders-to-erp",
  "version": 3,
  "confidence_threshold": 0.85,
  "allowed_actions": [
    {
      "action": "replay_message",
      "approval_required": false,
      "max_per_hour": 100,
      "enabled": true
    },
    {
      "action": "create_customer",
      "approval_required": true,
      "max_per_hour": 10,
      "enabled": true
    },
    {
      "action": "isolate_poison_message",
      "approval_required": false,
      "max_per_hour": 50,
      "enabled": true
    }
  ],
  "circuit_breaker": {
    "enabled": true,
    "failure_threshold": 5,
    "reset_timeout_minutes": 30,
    "half_open_requests": 3
  },
  "rate_limits": {
    "max_incidents_per_hour": 20,
    "max_actions_per_hour": 100
  },
  "notifications": {
    "teams_webhook": "https://outlook.office.com/webhook/...",
    "email_recipients": ["ops-team@company.com"],
    "notify_on_approval": true,
    "notify_on_completion": true,
    "notify_on_failure": true
  },
  "updated_at": "2026-02-10T12:00:00Z",
  "updated_by": "admin@company.com"
}
```

#### Update Policy
```http
PUT /api/policies/{integration_id}
Content-Type: application/json
```

**Request Body:**
```json
{
  "confidence_threshold": 0.90,
  "allowed_actions": [
    {
      "action": "replay_message",
      "approval_required": false,
      "max_per_hour": 200
    }
  ],
  "circuit_breaker": {
    "failure_threshold": 3
  }
}
```

---

### Incidents

#### List Incidents
```http
GET /api/incidents
```

**Query Parameters:**
- `hours` (optional): Last N hours (default: 24)
- `status` (optional): Filter by status (detected, analyzing, repairing, verified, closed, failed)
- `integration_id` (optional): Filter by integration
- `page` (optional): Page number (default: 1)
- `page_size` (optional): Items per page (default: 50, max: 100)

**Response:**
```json
{
  "incidents": [
    {
      "incident_id": "inc-2026-02-13-001",
      "integration_id": "orders-to-erp",
      "status": "closed",
      "detected_at": "2026-02-13T10:45:00Z",
      "resolved_at": "2026-02-13T10:57:00Z",
      "mttr_seconds": 720,
      "diagnosis": {
        "root_cause": "Customer CUS-12345 not found in ERP",
        "confidence": 0.92
      },
      "actions_taken": [
        {"action": "create_customer", "status": "success"},
        {"action": "replay_message", "status": "success"}
      ],
      "auto_resolved": true
    }
  ],
  "pagination": {
    "page": 1,
    "page_size": 50,
    "total_pages": 5,
    "total_items": 234
  }
}
```

#### Get Incident Details
```http
GET /api/incidents/{incident_id}
```

**Response:**
```json
{
  "incident_id": "inc-2026-02-13-001",
  "integration_id": "orders-to-erp",
  "status": "closed",
  "detected_at": "2026-02-13T10:45:00Z",
  "resolved_at": "2026-02-13T10:57:00Z",
  "mttr_seconds": 720,
  "orchestration_instance_id": "orch-abc123",
  
  "detection": {
    "trigger": "dlq_depth_threshold",
    "anomaly_score": 0.87,
    "dlq_depth": 47,
    "detected_by_agent": "watcher"
  },
  
  "diagnosis": {
    "root_cause_hypothesis": "Customer CUS-12345 not found in ERP system. Customer sync job failed 2 hours prior.",
    "confidence": 0.92,
    "risk_level": "medium",
    "evidence_citations": [
      "Service Bus DLQ message: 'Customer not found'",
      "Application Insights exception: CustomerNotFoundException at 10:30:15 UTC"
    ],
    "similar_incidents": 5,
    "diagnosed_at": "2026-02-13T10:48:00Z",
    "diagnosed_by_agent": "diagnostician"
  },
  
  "action_plan": {
    "actions": [
      {
        "sequence": 1,
        "action": "create_customer",
        "parameters": {"customer_id": "CUS-12345"},
        "estimated_duration_seconds": 30,
        "approval_required": true
      },
      {
        "sequence": 2,
        "action": "replay_messages",
        "parameters": {"count": 3, "filter": "correlationId = 'ORD-67890'"},
        "estimated_duration_seconds": 60
      }
    ],
    "total_estimated_duration_seconds": 90
  },
  
  "approval": {
    "required": true,
    "requested_at": "2026-02-13T10:49:00Z",
    "approved_at": "2026-02-13T10:52:00Z",
    "approved_by": "john.doe@company.com",
    "approval_method": "teams"
  },
  
  "execution": {
    "started_at": "2026-02-13T10:52:30Z",
    "completed_at": "2026-02-13T10:54:00Z",
    "actions": [
      {
        "action": "create_customer",
        "status": "success",
        "executed_at": "2026-02-13T10:52:30Z",
        "duration_seconds": 28,
        "result": {"customer_created": true, "customer_id": "CUS-12345"}
      },
      {
        "action": "replay_messages",
        "status": "success",
        "executed_at": "2026-02-13T10:53:00Z",
        "duration_seconds": 62,
        "result": {"messages_replayed": 3, "messages_succeeded": 3}
      }
    ]
  },
  
  "verification": {
    "verified_at": "2026-02-13T10:56:00Z",
    "verified_by_agent": "verifier",
    "outcome": "success",
    "business_validation": {
      "customer_exists": true,
      "orders_processed": 3,
      "no_duplicates": true
    },
    "confidence": 0.95
  },
  
  "learning": {
    "pattern_updated": true,
    "pattern_id": "pat-customer-missing",
    "new_success_rate": 0.96,
    "occurrence_count": 6
  },
  
  "auto_resolved": true,
  "rca_document_url": "https://storage.../rca/inc-2026-02-13-001.pdf"
}
```

#### Trigger Manual Incident
```http
POST /api/incidents/trigger
Content-Type: application/json
```

**Request Body:**
```json
{
  "integration_id": "orders-to-erp",
  "reason": "Manual investigation requested",
  "context": {
    "message_id": "msg-12345",
    "correlation_id": "ORD-67890"
  }
}
```

---

### Discovery

#### Trigger Discovery
```http
POST /api/discovery/trigger
Content-Type: application/json
```

**Request Body:**
```json
{
  "subscription_ids": [
    "12345678-1234-1234-1234-123456789012",
    "87654321-4321-4321-4321-210987654321"
  ],
  "tag_filter": "AutoHeal=Enabled",
  "force_refresh": false
}
```

**Response:**
```json
{
  "discovery_id": "disc-2026-02-13-001",
  "status": "running",
  "subscriptions_scanned": 2,
  "namespaces_found": 47,
  "integrations_discovered": 158,
  "started_at": "2026-02-13T11:00:00Z",
  "estimated_completion": "2026-02-13T11:05:00Z"
}
```

#### Get Discovery Status
```http
GET /api/discovery/{discovery_id}
```

---

### Runbooks

#### List Runbooks
```http
GET /api/runbooks
```

**Response:**
```json
{
  "runbooks": [
    {
      "action_name": "replay_message",
      "display_name": "Replay Dead Letter Message",
      "risk_level": "low",
      "approval_required": false,
      "category": "message_handling",
      "parameters_schema": {
        "type": "object",
        "properties": {
          "message_id": {"type": "string"},
          "correlation_id": {"type": "string"},
          "batch_size": {"type": "integer", "default": 1}
        }
      },
      "verification_criteria": [
        "message_consumed",
        "no_errors_logged",
        "dlq_depth_decreased"
      ]
    }
  ]
}
```

#### Register Custom Runbook
```http
POST /api/runbooks
Content-Type: application/json
```

**Request Body:**
```json
{
  "action_name": "custom_data_fix",
  "display_name": "Fix Custom Data Issue",
  "risk_level": "medium",
  "approval_required": true,
  "category": "data_correction",
  "parameters_schema": {
    "type": "object",
    "properties": {
      "entity_id": {"type": "string", "required": true},
      "field_name": {"type": "string", "required": true},
      "new_value": {"type": "string", "required": true}
        }
  },
  "executor_function": "CustomDataFixFunction",
  "verification_criteria": [
    "entity_updated",
    "audit_logged"
  ]
}
```

---

## Query API

### Analytics

#### Get Incident Statistics
```http
GET /api/analytics/incidents
```

**Query Parameters:**
- `start_date`: ISO 8601 date (default: 30 days ago)
- `end_date`: ISO 8601 date (default: now)
- `integration_id` (optional): Filter by integration
- `group_by` (optional): hour, day, week, month

**Response:**
```json
{
  "period": {
    "start": "2026-01-14T00:00:00Z",
    "end": "2026-02-13T00:00:00Z"
  },
  "summary": {
    "total_incidents": 234,
    "auto_resolved": 187,
    "manual_intervention": 47,
    "auto_resolution_rate": 0.799,
    "avg_mttr_seconds": 420,
    "median_mttr_seconds": 180
  },
  "by_integration": [
    {
      "integration_id": "orders-to-erp",
      "incident_count": 47,
      "auto_resolution_rate": 0.85,
      "avg_mttr_seconds": 360
    }
  ],
  "by_root_cause": [
    {
      "category": "missing_master_data",
      "count": 89,
      "percentage": 0.38
    },
    {
      "category": "downstream_timeout",
      "count": 56,
      "percentage": 0.24
    }
  ],
  "trend": [
    {
      "date": "2026-01-14",
      "incidents": 8,
      "auto_resolved": 6
    }
  ]
}
```

#### Get Confidence Calibration Metrics
```http
GET /api/analytics/confidence
```

**Response:**
```json
{
  "overall_accuracy": 0.91,
  "by_confidence_bucket": [
    {
      "confidence_range": "0.90-1.00",
      "predictions": 120,
      "correct": 115,
      "accuracy": 0.958,
      "calibration_delta": 0.038
    },
    {
      "confidence_range": "0.80-0.90",
      "predictions": 78,
      "correct": 67,
      "accuracy": 0.859,
      "calibration_delta": 0.009
    }
  ],
  "calibration_curve": [
    {"predicted": 0.95, "actual": 0.96},
    {"predicted": 0.85, "actual": 0.86}
  ]
}
```

---

## Webhooks

### Register Webhook
```http
POST /api/webhooks
Content-Type: application/json
```

**Request Body:**
```json
{
  "url": "https://your-app.com/webhooks/continuum-ops",
  "events": [
    "incident.detected",
    "incident.diagnosed",
    "incident.approval_required",
    "incident.resolved",
    "incident.failed"
  ],
  "integration_id": "orders-to-erp",
  "secret": "your-webhook-secret"
}
```

### Webhook Payload Format

**Event: incident.approval_required**
```json
{
  "event": "incident.approval_required",
  "timestamp": "2026-02-13T10:49:00Z",
  "incident_id": "inc-2026-02-13-001",
  "integration_id": "orders-to-erp",
  "diagnosis": {
    "root_cause": "Customer CUS-12345 not found",
    "confidence": 0.92,
    "risk_level": "medium"
  },
  "proposed_actions": [
    {
      "action": "create_customer",
      "parameters": {"customer_id": "CUS-12345"}
    }
  ],
  "approval_url": "https://func-continuumops.../api/approvals/appr-001"
}
```

### Webhook Signature Verification

```csharp
// C# example
public bool VerifyWebhookSignature(string payload, string signature, string secret)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    var computedSignature = Convert.ToBase64String(hash);
    return signature == computedSignature;
}
```

---

## SDKs

### .NET SDK

**Installation:**
```bash
dotnet add package ContinuumOps.Sdk
```

**Usage:**
```csharp
using Azure.Identity;
using ContinuumOps.Sdk;

var client = new ContinuumOpsClient(
    new Uri("https://func-continuumops-prod-eastus.azurewebsites.net"),
    new DefaultAzureCredential()
);

// List recent incidents
var incidents = await client.Incidents.ListAsync(hours: 24);
foreach (var incident in incidents)
{
    Console.WriteLine($"{incident.IncidentId}: {incident.Status} (MTTR: {incident.MttrSeconds}s)");
}

// Register custom runbook
await client.Runbooks.RegisterAsync(new Runbook
{
    ActionName = "custom-fix",
    RiskLevel = RiskLevel.Medium,
    ApprovalRequired = true,
    Executor = async (context) =>
    {
        // Your custom logic
        await FixDataAsync(context.Parameters);
    }
});
```

### TypeScript/JavaScript SDK

**Installation:**
```bash
npm install @continuum-ops/sdk
```

**Usage:**
```typescript
import { ContinuumOpsClient, DefaultAzureCredential } from '@continuum-ops/sdk';

const client = new ContinuumOpsClient({
  endpoint: 'https://func-continuumops-prod-eastus.azurewebsites.net',
  credential: new DefaultAzureCredential()
});

// List integrations
const integrations = await client.integrations.list();
console.log(`Found ${integrations.length} integrations`);

// Get incident details
const incident = await client.incidents.get('inc-2026-02-13-001');
console.log(`Root cause: ${incident.diagnosis.rootCause}`);
```

### Python SDK

**Installation:**
```bash
pip install continuum-ops-sdk
```

**Usage:**
```python
from azure.identity import DefaultAzureCredential
from continuum_ops import ContinuumOpsClient

client = ContinuumOpsClient(
    endpoint="https://func-continuumops-prod-eastus.azurewebsites.net",
    credential=DefaultAzureCredential()
)

# List recent incidents
incidents = client.incidents.list(hours=24)
for incident in incidents:
    print(f"{incident.incident_id}: {incident.status}")

# Update policy
client.policies.update(
    integration_id="orders-to-erp",
    confidence_threshold=0.90,
    allowed_actions=[
        {"action": "replay_message", "approval_required": False}
    ]
)
```

---

## Rate Limits

| Tier | Requests/Minute | Burst Limit |
|------|-----------------|-------------|
| **Standard** | 300 | 500 |
| **High Scale** | 1000 | 2000 |

**Rate Limit Headers:**
```
X-RateLimit-Limit: 300
X-RateLimit-Remaining: 287
X-RateLimit-Reset: 1707825600
```

---

## Error Codes

| Code | Message | Description |
|------|---------|-------------|
| 400 | Bad Request | Invalid request parameters |
| 401 | Unauthorized | Missing or invalid authentication |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Resource not found |
| 409 | Conflict | Resource already exists |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Platform error |
| 503 | Service Unavailable | Temporary outage |

**Error Response Format:**
```json
{
  "error": {
    "code": "InvalidParameter",
    "message": "The parameter 'confidence_threshold' must be between 0.70 and 0.95",
    "details": {
      "parameter": "confidence_threshold",
      "provided_value": 0.65,
      "valid_range": [0.70, 0.95]
    },
    "trace_id": "00-abc123-def456-01"
  }
}
```

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-13 | Initial API reference |

---

**© 2026 Continuum-Ops**
