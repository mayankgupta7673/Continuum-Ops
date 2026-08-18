# Continuum-Ops: Ticketing System Integration Strategy
## ADO, JIRA, ServiceNow Integration Guide

**Last Updated:** August 18, 2026  
**Status:** Recommended Approach

---

## Executive Summary

**Question:** Should we integrate Azure DevOps (ADO), JIRA, or ServiceNow from day 1 of the POC?

**Recommendation:** **Yes, but keep it simple.** Add basic incident logging to prove audit trail and stakeholder visibility, but don't build complex bi-directional sync in the POC phase.

### Integration Philosophy

```
POC Phase (Weeks 1-8):          Post-POC (Production):
┌─────────────────────┐         ┌──────────────────────────┐
│ Basic Integration   │         │ Full Bi-Directional Sync │
│                     │         │                          │
│ ✅ Create tickets   │         │ ✅ Create/update tickets │
│ ✅ Add comments     │         │ ✅ Status sync           │
│ ✅ Close tickets    │         │ ✅ Assignment routing    │
│                     │         │ ✅ SLA tracking          │
│ ❌ No status sync   │         │ ✅ Custom field mapping  │
│ ❌ No webhooks      │         │ ✅ Webhook listeners     │
│ ❌ No complex flow  │         │ ✅ Approval workflows    │
└─────────────────────┘         └──────────────────────────┘

Time Investment: 3-5 days       Time Investment: 2-3 weeks
```

---

## Why Integrate Ticketing Systems?

### Benefits

| Benefit | Business Value | POC Priority |
|---------|---------------|--------------|
| **Audit Trail** | Compliance requirement, immutable incident record | 🔥 Critical |
| **Stakeholder Visibility** | Management can track incidents in familiar tools | 🔥 Critical |
| **Historical Analysis** | Trend analysis, reporting, metrics | ⚠️ Important |
| **Team Collaboration** | Engineers can add notes, escalate | ⚠️ Important |
| **Integration with Runbooks** | Link to existing documentation | ✅ Nice to have |
| **SLA Tracking** | Measure time to resolution against targets | ✅ Nice to have |

### Risks of Over-Integration in POC

| Risk | Impact | Mitigation |
|------|--------|-----------|
| **Increased Complexity** | POC timeline extends from 8 weeks to 10-12 weeks | Limit scope to basic CRUD operations |
| **External Dependencies** | JIRA/ServiceNow API changes break POC | Use well-established APIs, version pin |
| **Authentication Overhead** | Managing API keys, OAuth tokens adds friction | Use Managed Identity where possible |
| **Distraction from Core Value** | Team focuses on integration, not AI agents | Timebox integration work to 3-5 days max |

---

## Recommended Approach: Phased Integration

### Phase 1: POC (Weeks 1-8) - Basic Logging

**Goal:** Prove incident visibility and audit compliance

**Scope:**
- ✅ Create ticket when incident detected
- ✅ Add diagnosis results as comment
- ✅ Add repair actions as comments
- ✅ Close ticket when verified
- ✅ Tag with metadata (severity, category, integration)

**Do NOT build:**
- ❌ Bi-directional status sync
- ❌ Webhook listeners from ticketing system
- ❌ Complex field mapping
- ❌ Assignment logic
- ❌ Custom workflows

**Time Investment:** 3-5 days (Week 2 or Week 6)

**Value:** Stakeholders can see incidents in their existing tools without learning new UI

---

### Phase 2: Post-POC (Weeks 9-12) - Enhanced Integration

**Goal:** Full operational integration

**Scope:**
- ✅ Bi-directional status sync (Continuum-Ops ↔ Ticketing System)
- ✅ Webhook listeners for manual ticket updates
- ✅ Assignment routing based on category/severity
- ✅ SLA tracking and alerting
- ✅ Custom field mapping per organization
- ✅ Integration with existing approval workflows
- ✅ Link to runbooks and documentation

**Time Investment:** 2-3 weeks

---

## Implementation Guide: Azure DevOps (ADO)

### Why ADO First?

- ✅ **Native Azure Integration** - Managed Identity, same tenant
- ✅ **REST API Maturity** - Well-documented, stable
- ✅ **Common in .NET Shops** - Likely already in use
- ✅ **Free Tier** - No additional cost for basic usage

### POC Integration (Basic)

**File: `Continuum.Ops.Functions/Services/AdoTicketingService.cs`**

```csharp
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System.Text.Json;

namespace Continuum.Ops.Functions.Services;

public class AdoTicketingService : ITicketingService
{
    private readonly WorkItemTrackingHttpClient _witClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdoTicketingService> _logger;

    public AdoTicketingService(
        IConfiguration configuration,
        ILogger<AdoTicketingService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var orgUrl = configuration["AzureDevOps:OrganizationUrl"]!;
        var personalAccessToken = configuration["AzureDevOps:PAT"]!;

        var credentials = new VssBasicCredential(string.Empty, personalAccessToken);
        var connection = new VssConnection(new Uri(orgUrl), credentials);
        
        _witClient = connection.GetClient<WorkItemTrackingHttpClient>();
    }

    public async Task<string> CreateIncidentTicketAsync(
        string incidentId,
        IncidentDetails details,
        CancellationToken cancellationToken = default)
    {
        var project = _configuration["AzureDevOps:Project"]!;

        // Build work item fields
        var patchDocument = new JsonPatchDocument
        {
            new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.Title",
                Value = $"[Continuum-Ops] {details.Title}"
            },
            new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.Description",
                Value = BuildDescription(incidentId, details)
            },
            new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.Tags",
                Value = "Continuum-Ops; AutoHeal; Service-Bus"
            },
            new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/Microsoft.VSTS.Common.Priority",
                Value = details.Severity switch
                {
                    "Critical" => 1,
                    "High" => 2,
                    "Medium" => 3,
                    _ => 4
                }
            }
        };

        // Create work item
        var workItem = await _witClient.CreateWorkItemAsync(
            patchDocument,
            project,
            "Bug",  // Or "Incident" if you have custom type
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Created ADO work item {WorkItemId} for incident {IncidentId}",
            workItem.Id, incidentId);

        return workItem.Id.ToString()!;
    }

    public async Task AddCommentAsync(
        string ticketId,
        string comment,
        CancellationToken cancellationToken = default)
    {
        var patchDocument = new JsonPatchDocument
        {
            new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.History",
                Value = comment
            }
        };

        await _witClient.UpdateWorkItemAsync(
            patchDocument,
            int.Parse(ticketId),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Added comment to ADO work item {WorkItemId}", ticketId);
    }

    public async Task CloseTicketAsync(
        string ticketId,
        string resolution,
        CancellationToken cancellationToken = default)
    {
        var patchDocument = new JsonPatchDocument
        {
            new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.State",
                Value = "Resolved"
            },
            new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.History",
                Value = $"✅ Resolved by Continuum-Ops\\n\\n{resolution}"
            }
        };

        await _witClient.UpdateWorkItemAsync(
            patchDocument,
            int.Parse(ticketId),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Closed ADO work item {WorkItemId}", ticketId);
    }

    private string BuildDescription(string incidentId, IncidentDetails details)
    {
        return $"""
            **Incident ID:** {incidentId}
            **Detection Time:** {details.DetectionTime:yyyy-MM-dd HH:mm:ss} UTC
            **Namespace:** {details.NamespaceName}
            **Queue/Topic:** {details.QueueName}
            **Error Signature:** {details.ErrorSignature}
            
            **Automated Actions:**
            This incident was detected and will be processed by Continuum-Ops AI agents.
            
            - 🧠 Diagnosis Agent will analyze root cause
            - 🔧 Repair Agent will execute remediation (if confidence is high)
            - ✅ Verify Agent will validate the outcome
            
            All actions will be logged as comments to this work item.
            
            **Links:**
            - [View in Continuum-Ops Dashboard](https://portal.azure.com)
            - [Application Insights](https://portal.azure.com)
            
            ---
            *Generated by Continuum-Ops v1.0 - Autonomous Incident Response*
            """;
    }
}

public interface ITicketingService
{
    Task<string> CreateIncidentTicketAsync(string incidentId, IncidentDetails details, CancellationToken cancellationToken = default);
    Task AddCommentAsync(string ticketId, string comment, CancellationToken cancellationToken = default);
    Task CloseTicketAsync(string ticketId, string resolution, CancellationToken cancellationToken = default);
}

public class IncidentDetails
{
    public string Title { get; set; } = string.Empty;
    public string NamespaceName { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public string ErrorSignature { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public DateTime DetectionTime { get; set; } = DateTime.UtcNow;
}
```

### Configuration

**Update `local.settings.json`:**

```json
{
  "Values": {
    "AzureDevOps__OrganizationUrl": "https://dev.azure.com/your-org",
    "AzureDevOps__Project": "YourProjectName",
    "AzureDevOps__PAT": "your-personal-access-token"
  }
}
```

### NuGet Packages

```bash
dotnet add package Microsoft.TeamFoundationServer.Client --version 19.225.1
dotnet add package Microsoft.VisualStudio.Services.Client --version 19.225.1
```

### Integration in Orchestrator

**Update: `IncidentOrchestrator.cs`**

```csharp
[Function(nameof(IncidentOrchestrator))]
public async Task<string> RunOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context,
    string alertData)
{
    var incidentId = context.NewGuid().ToString();
    
    // 1. Create ticket immediately for visibility
    var ticketId = await context.CallActivityAsync<string>(
        nameof(CreateTicketActivity),
        new { IncidentId = incidentId, AlertData = alertData });
    
    // 2. Diagnose
    var diagnosis = await context.CallActivityAsync<DiagnosisResult>(
        nameof(DiagnoseIncidentActivity),
        new { IncidentId = incidentId, AlertData = alertData });
    
    // 3. Add diagnosis to ticket
    await context.CallActivityAsync(
        nameof(AddTicketCommentActivity),
        new { 
            TicketId = ticketId, 
            Comment = FormatDiagnosis(diagnosis) 
        });
    
    // 4. Repair (if approved)
    if (diagnosis.Confidence >= 0.85)
    {
        var repairResult = await context.CallActivityAsync<RepairResult>(
            nameof(RepairIncidentActivity),
            diagnosis.RepairPlan);
        
        await context.CallActivityAsync(
            nameof(AddTicketCommentActivity),
            new { 
                TicketId = ticketId, 
                Comment = FormatRepairResult(repairResult) 
            });
        
        // 5. Verify
        var verifyResult = await context.CallActivityAsync<VerificationResult>(
            nameof(VerifyIncidentActivity),
            new { Diagnosis = diagnosis, RepairResult = repairResult });
        
        // 6. Close ticket if successful
        if (verifyResult.Verified)
        {
            await context.CallActivityAsync(
                nameof(CloseTicketActivity),
                new { 
                    TicketId = ticketId, 
                    Resolution = FormatVerification(verifyResult) 
                });
        }
    }
    
    return incidentId;
}
```

---

## Implementation Guide: JIRA

### Why JIRA?

- ✅ **Industry Standard** - Widely used across enterprises
- ✅ **Rich API** - Comprehensive REST API with good documentation
- ✅ **Customizable** - Custom fields, workflows, issue types
- ⚠️ **Authentication** - API tokens or OAuth 2.0 (more complex than ADO)

### POC Integration (Basic)

**File: `Continuum.Ops.Functions/Services/JiraTicketingService.cs`**

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Continuum.Ops.Functions.Services;

public class JiraTicketingService : ITicketingService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JiraTicketingService> _logger;

    public JiraTicketingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<JiraTicketingService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Jira");

        // Setup authentication
        var email = configuration["Jira:Email"]!;
        var apiToken = configuration["Jira:ApiToken"]!;
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", authValue);
        _httpClient.BaseAddress = new Uri(configuration["Jira:BaseUrl"]!);
    }

    public async Task<string> CreateIncidentTicketAsync(
        string incidentId,
        IncidentDetails details,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            fields = new
            {
                project = new { key = _configuration["Jira:ProjectKey"] },
                summary = $"[Continuum-Ops] {details.Title}",
                description = BuildJiraDescription(incidentId, details),
                issuetype = new { name = "Bug" },  // Or "Incident"
                priority = new { name = details.Severity },
                labels = new[] { "continuum-ops", "autoheal", "service-bus" }
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            "/rest/api/3/issue",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JiraIssueResponse>(cancellationToken);
        
        _logger.LogInformation(
            "Created JIRA issue {IssueKey} for incident {IncidentId}",
            result!.Key, incidentId);

        return result.Key;
    }

    public async Task AddCommentAsync(
        string ticketId,
        string comment,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            body = new
            {
                type = "doc",
                version = 1,
                content = new[]
                {
                    new
                    {
                        type = "paragraph",
                        content = new[]
                        {
                            new { type = "text", text = comment }
                        }
                    }
                }
            }
        };

        await _httpClient.PostAsJsonAsync(
            $"/rest/api/3/issue/{ticketId}/comment",
            payload,
            cancellationToken);

        _logger.LogInformation("Added comment to JIRA issue {IssueKey}", ticketId);
    }

    public async Task CloseTicketAsync(
        string ticketId,
        string resolution,
        CancellationToken cancellationToken = default)
    {
        // First, add final comment
        await AddCommentAsync(
            ticketId,
            $"✅ Resolved by Continuum-Ops\\n\\n{resolution}",
            cancellationToken);

        // Then transition to closed state
        var payload = new
        {
            transition = new { id = "31" }  // "Done" transition ID (varies by JIRA config)
        };

        await _httpClient.PostAsJsonAsync(
            $"/rest/api/3/issue/{ticketId}/transitions",
            payload,
            cancellationToken);

        _logger.LogInformation("Closed JIRA issue {IssueKey}", ticketId);
    }

    private string BuildJiraDescription(string incidentId, IncidentDetails details)
    {
        return $"""
            *Incident ID:* {incidentId}
            *Detection Time:* {details.DetectionTime:yyyy-MM-dd HH:mm:ss} UTC
            *Namespace:* {details.NamespaceName}
            *Queue/Topic:* {details.QueueName}
            
            h2. Automated Processing
            
            This incident is being handled by Continuum-Ops AI agents:
            * 🧠 Diagnosis Agent - Analyzing root cause
            * 🔧 Repair Agent - Executing remediation
            * ✅ Verify Agent - Validating outcome
            
            All actions will be added as comments.
            """;
    }
}

public class JiraIssueResponse
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Self { get; set; } = string.Empty;
}
```

### Configuration

```json
{
  "Jira__BaseUrl": "https://your-company.atlassian.net",
  "Jira__Email": "your-email@company.com",
  "Jira__ApiToken": "your-jira-api-token",
  "Jira__ProjectKey": "PROJ"
}
```

---

## Implementation Guide: ServiceNow

### Why ServiceNow?

- ✅ **Enterprise ITSM** - Standard for large enterprises
- ✅ **ITIL Alignment** - Incident, problem, change management
- ✅ **Integration Hub** - Built-in connectors for many systems
- ⚠️ **Cost** - Often requires ServiceNow licenses
- ⚠️ **Complexity** - More complex API than ADO/JIRA

### POC Integration (Basic)

**File: `Continuum.Ops.Functions/Services/ServiceNowTicketingService.cs`**

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Continuum.Ops.Functions.Services;

public class ServiceNowTicketingService : ITicketingService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServiceNowTicketingService> _logger;

    public ServiceNowTicketingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ServiceNowTicketingService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("ServiceNow");

        // Setup authentication
        var username = configuration["ServiceNow:Username"]!;
        var password = configuration["ServiceNow:Password"]!;
        var authValue = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{username}:{password}"));
        
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", authValue);
        _httpClient.BaseAddress = new Uri(configuration["ServiceNow:InstanceUrl"]!);
    }

    public async Task<string> CreateIncidentTicketAsync(
        string incidentId,
        IncidentDetails details,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            short_description = $"[Continuum-Ops] {details.Title}",
            description = BuildServiceNowDescription(incidentId, details),
            urgency = details.Severity switch
            {
                "Critical" => "1",
                "High" => "2",
                "Medium" => "3",
                _ => "4"
            },
            impact = "2",  // Medium impact
            category = "Integration",
            subcategory = "Service Bus",
            assignment_group = _configuration["ServiceNow:AssignmentGroup"],
            caller_id = _configuration["ServiceNow:CallerId"]
        };

        var response = await _httpClient.PostAsJsonAsync(
            "/api/now/table/incident",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ServiceNowIncidentResponse>(
            cancellationToken);
        
        _logger.LogInformation(
            "Created ServiceNow incident {IncidentNumber} for {IncidentId}",
            result!.Result.Number, incidentId);

        return result.Result.SysId;
    }

    public async Task AddCommentAsync(
        string ticketId,
        string comment,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            work_notes = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] {comment}"
        };

        await _httpClient.PatchAsJsonAsync(
            $"/api/now/table/incident/{ticketId}",
            payload,
            cancellationToken);

        _logger.LogInformation("Added work note to ServiceNow incident {SysId}", ticketId);
    }

    public async Task CloseTicketAsync(
        string ticketId,
        string resolution,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            state = "6",  // Resolved
            close_code = "Solved (Permanently)",
            close_notes = $"✅ Resolved by Continuum-Ops\\n\\n{resolution}",
            resolved_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };

        await _httpClient.PatchAsJsonAsync(
            $"/api/now/table/incident/{ticketId}",
            payload,
            cancellationToken);

        _logger.LogInformation("Closed ServiceNow incident {SysId}", ticketId);
    }

    private string BuildServiceNowDescription(string incidentId, IncidentDetails details)
    {
        return $"""
            Incident ID: {incidentId}
            Detection Time: {details.DetectionTime:yyyy-MM-dd HH:mm:ss} UTC
            Namespace: {details.NamespaceName}
            Queue/Topic: {details.QueueName}
            Error Signature: {details.ErrorSignature}
            
            === AUTOMATED PROCESSING ===
            
            This incident is being processed by Continuum-Ops autonomous healing platform:
            
            1. Diagnosis Agent - Analyzing root cause with AI
            2. Repair Agent - Executing remediation actions
            3. Verify Agent - Validating resolution
            
            All actions will be logged as work notes.
            
            For questions, contact: Platform Engineering Team
            """;
    }
}

public class ServiceNowIncidentResponse
{
    public ServiceNowIncident Result { get; set; } = new();
}

public class ServiceNowIncident
{
    [JsonPropertyName("sys_id")]
    public string SysId { get; set; } = string.Empty;
    
    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;
}
```

---

## Recommended POC Configuration

### Which System to Choose?

**Decision Matrix:**

| Your Situation | Recommendation | Rationale |
|----------------|---------------|-----------|
| **Using Azure DevOps already** | Start with ADO | Native integration, Managed Identity support |
| **Enterprise with ServiceNow** | Start with ServiceNow | Shows enterprise readiness, ITIL compliance |
| **Multi-platform development** | Start with JIRA | Flexible, widely understood |
| **Want to demonstrate flexibility** | Implement interface + 2 providers | Shows architecture quality |

### POC Recommendation: Pluggable Architecture

Implement the `ITicketingService` interface with 2 providers (e.g., ADO + JIRA). Configure via settings:

```json
{
  "Ticketing__Provider": "AzureDevOps",  // or "Jira" or "ServiceNow"
  "Ticketing__Enabled": true
}
```

**Benefits:**
- Demonstrates architecture quality
- Easy to switch providers post-POC
- Shows multi-tenant potential

**Time Investment:** +2 days (vs single provider)

---

## Integration Timeline

### Week 2 (Recommended): Early Integration
**Pros:**
- Stakeholder visibility from day 1 of testing
- Audit trail established early
- Time to iterate if issues arise

**Cons:**
- Slight distraction from core agent development

**Recommendation:** Do this if stakeholders are asking "where can I see incidents?"

### Week 6 (Alternative): Late Integration
**Pros:**
- Focus on core value first
- Integrate only if POC is successful
- Less risk of timeline slip

**Cons:**
- No stakeholder visibility until late
- Rushed implementation if issues arise

**Recommendation:** Do this if team is stretched thin

---

## Post-POC Enhancement Roadmap

### Phase 2: Bi-Directional Sync (Weeks 9-11)

**Goals:**
- Sync status changes from ticketing system to Continuum-Ops
- Allow manual override/escalation from tickets
- Support custom workflows per organization

**Implementation:**
- Webhook listeners (ADO service hooks, JIRA webhooks, ServiceNow outbound REST)
- State machine for incident status
- Conflict resolution logic

### Phase 3: Advanced Features (Weeks 12-14)

- Custom field mapping per organization
- Integration with existing approval workflows
- SLA tracking and alerting
- Runbook linking
- Knowledge base integration
- Trend analysis and reporting

---

## Cost Implications

### API Costs

| System | API Cost | POC Impact | Notes |
|--------|----------|-----------|-------|
| **Azure DevOps** | Free (included in Azure subscription) | $0 | Unlimited API calls |
| **JIRA Cloud** | Free tier: 10K API calls/day | $0 | Well within POC limits |
| **ServiceNow** | Depends on license | Variable | Check with SNOW admin |

### Development Time

| Phase | Effort | Team | Timeline |
|-------|--------|------|----------|
| POC Basic Integration | 3-5 days | Backend Dev (50%) | Week 2 or 6 |
| Post-POC Full Integration | 2-3 weeks | Backend Dev (100%) + Integration Specialist (50%) | Weeks 9-11 |

---

## Security Considerations

### Authentication Methods

| System | Recommended Auth | POC Auth | Production Auth |
|--------|-----------------|----------|-----------------|
| **Azure DevOps** | Managed Identity | PAT | Managed Identity via Azure |
| **JIRA** | OAuth 2.0 | API Token | OAuth 2.0 with refresh tokens |
| **ServiceNow** | OAuth 2.0 | Basic Auth | OAuth 2.0 with client credentials |

### Secret Management

**POC:**
- Store credentials in Azure Key Vault
- Reference from Function App configuration

**Production:**
- Use Managed Identity where possible
- Rotate credentials regularly (30-90 days)
- Implement least-privilege access

---

## Success Metrics

### POC Evaluation Criteria

- ✅ **Ticket Creation Success Rate:** >99%
- ✅ **Average Ticket Creation Latency:** <5 seconds
- ✅ **Comment Update Success Rate:** >99%
- ✅ **Stakeholder Satisfaction:** >4/5 ("I can see incidents in my familiar tool")
- ✅ **Audit Compliance:** 100% incidents logged

---

## Decision: Should You Integrate for POC?

### ✅ **YES, integrate if:**
- Management/stakeholders need visibility in existing tools
- Audit compliance requires ticketing system records
- You have 3-5 days to spare in POC timeline
- You want to demonstrate architecture quality

### ❌ **NO, skip if:**
- POC timeline is already tight
- Cosmos DB audit trail is sufficient for now
- Stakeholders are comfortable with custom dashboards
- Focus is purely on AI agent proof-of-concept

### 🎯 **My Recommendation:**

**Do basic ADO or JIRA integration in Week 6** (after agents are working). 

**Why?**
- Proves audit trail and visibility
- Doesn't distract from core AI development
- Takes only 3-4 days
- Big win for management presentation
- Easy to extend post-POC

**Specific scope for POC:**
```
✅ Create ticket on incident detection
✅ Add comments for diagnosis, repair, verification
✅ Close ticket on successful resolution
❌ Skip: Status sync, webhooks, custom workflows
```

---

**Questions?** Contact the Platform Engineering team.

**Document Owner:** [Your Name]  
**Last Updated:** August 18, 2026  
**Version:** 1.0
