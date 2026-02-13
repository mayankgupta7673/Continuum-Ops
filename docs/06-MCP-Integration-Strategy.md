# MCP Integration Strategy

## Overview

This document details how **Model Context Protocol (MCP) servers** can be integrated into Continuum-Ops to enhance resource discovery, evidence collection, and AI-agent capabilities beyond traditional Azure Management APIs.

---

## What is Model Context Protocol (MCP)?

**Model Context Protocol** is an open standard that enables:

1. **Structured context exposure**: Systems expose data and capabilities as tools/resources
2. **AI agent integration**: LLMs can discover and invoke tools dynamically
3. **Standardized interaction**: Common protocol for client-server communication
4. **Composability**: Mix and match MCP servers from different providers

### MCP Components

- **MCP Server**: Exposes tools and resources (e.g., Azure resource APIs, database queries)
- **MCP Client**: Consumes tools (e.g., Azure OpenAI agents, custom automation)
- **Tools**: Functions that can be invoked with parameters
- **Resources**: Data sources that can be queried
- **Prompts**: Pre-defined prompt templates with context

---

## MCP vs. Azure Management APIs

### Traditional Approach (Azure Management REST APIs)

```csharp
// Direct Azure SDK call
var serviceBusClient = new ServiceBusAdministrationClient(connectionString);
var queues = await serviceBusClient.GetQueuesAsync();
foreach (var queue in queues)
{
    var runtimeProperties = await serviceBusClient.GetQueueRuntimePropertiesAsync(queue.Name);
    Console.WriteLine($"Queue: {queue.Name}, DLQ Count: {runtimeProperties.DeadLetterMessageCount}");
}
```

**Limitations:**
- Requires custom code for each resource type
- No standardized schema for AI agents
- Hard to extend or compose
- Tight coupling to Azure SDK versions

### MCP Approach

```json
// MCP tool invocation (sent to Azure OpenAI via function calling)
{
  "tool": "azure_servicebus_list_queues_with_metrics",
  "parameters": {
    "namespace": "contoso-sb.servicebus.windows.net",
    "include_dlq_metrics": true
  }
}

// MCP server response
{
  "result": [
    {
      "name": "orders-queue",
      "active_messages": 120,
      "dead_letter_messages": 47,
      "size_in_bytes": 1048576,
      "last_updated": "2026-02-12T10:30:00Z"
    }
  ]
}
```

**Advantages:**
- Standardized schema for AI consumption
- Composable (mix Azure, ERP, custom tools)
- Versionable and discoverable
- Decoupled from Function App code (can be external service)

---

## MCP Use Cases in Continuum-Ops

### 1. Resource Discovery (Module 10: Policy & Configuration)

**Problem:** Need to discover all Service Bus queues, topics, and subscriptions across multiple namespaces and subscriptions.

**MCP Solution:**

**Tool:** `azure_servicebus_discover_entities`

**Parameters:**
```json
{
  "subscription_ids": ["sub-1", "sub-2"],
  "resource_tag_filter": { "autoheal": "enabled" },
  "include_dlq_metrics": true
}
```

**Response:**
```json
{
  "entities": [
    {
      "subscription_id": "sub-1",
      "resource_group": "rg-integrations",
      "namespace": "contoso-sb",
      "type": "queue",
      "name": "orders-queue",
      "dlq_message_count": 47,
      "tags": { "autoheal": "enabled", "owner": "erp-team" }
    },
    {
      "type": "topic",
      "name": "events-topic",
      "subscriptions": [
        { "name": "processor-sub", "dlq_message_count": 12 }
      ]
    }
  ]
}
```

**Integration Point:** Module 10 calls this MCP tool on a schedule (e.g., hourly) to auto-discover new integrations and update the registry.

---

### 2. Evidence Collection (Module 3: Diagnosis Agent)

**Problem:** Need to query Application Insights and Log Analytics for correlated traces, exceptions, and dependency failures.

**MCP Solution:**

**Tool:** `azure_insights_query_correlated_events`

**Parameters:**
```json
{
  "workspace_id": "...",
  "correlation_id": "ORD-12345",
  "time_range": "PT1H",
  "event_types": ["traces", "exceptions", "dependencies"]
}
```

**Response:**
```json
{
  "events": [
    {
      "type": "exception",
      "timestamp": "2026-02-12T09:45:32Z",
      "message": "Customer ID CUS-12345 not found in ERP",
      "operation_name": "ProcessOrder",
      "severity": "Error"
    },
    {
      "type": "dependency",
      "timestamp": "2026-02-12T09:45:31Z",
      "target": "erp-api.contoso.com",
      "name": "GET /api/customers/CUS-12345",
      "result_code": "404",
      "duration": "250ms"
    }
  ]
}
```

**Integration Point:** Diagnosis Agent calls this MCP tool and includes the structured evidence in the AI prompt for root cause analysis.

---

### 3. Dynamic Tool Selection by AI (Azure OpenAI Function Calling)

**Problem:** Let Azure OpenAI decide which tools to use based on incident context.

**MCP Solution:**

When calling Azure OpenAI, provide MCP tools as function definitions:

```json
{
  "model": "gpt-4",
  "messages": [
    {
      "role": "system",
      "content": "You are an integration reliability expert. Use available tools to diagnose the incident."
    },
    {
      "role": "user",
      "content": "Order ORD-12345 is stuck in DLQ. Diagnose the issue."
    }
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "azure_servicebus_peek_dlq_message",
        "description": "Retrieve a message from the dead-letter queue for inspection",
        "parameters": {
          "type": "object",
          "properties": {
            "namespace": { "type": "string" },
            "entity_path": { "type": "string" },
            "message_id": { "type": "string" }
          },
          "required": ["namespace", "entity_path"]
        }
      }
    },
    {
      "type": "function",
      "function": {
        "name": "azure_insights_query_correlated_events",
        "description": "Query Application Insights for correlated traces and exceptions",
        "parameters": { ... }
      }
    },
    {
      "type": "function",
      "function": {
        "name": "erp_get_customer",
        "description": "Retrieve customer details from ERP by ID",
        "parameters": {
          "type": "object",
          "properties": {
            "customer_id": { "type": "string" }
          },
          "required": ["customer_id"]
        }
      }
    }
  ]
}
```

**AI Response (function call decision):**
```json
{
  "role": "assistant",
  "content": null,
  "tool_calls": [
    {
      "id": "call_1",
      "type": "function",
      "function": {
        "name": "azure_servicebus_peek_dlq_message",
        "arguments": "{\"namespace\":\"contoso-sb\",\"entity_path\":\"orders-queue\"}"
      }
    }
  ]
}
```

**Flow:**
1. Diagnosis Agent invokes the MCP server to execute `azure_servicebus_peek_dlq_message`
2. MCP server returns message body and headers
3. Diagnosis Agent sends result back to OpenAI
4. OpenAI analyzes and may call additional tools (e.g., `erp_get_customer`)
5. Final diagnosis is produced with evidence citations

---

### 4. Master Data Validation and Creation (Module 5: Repair Agent)

**Problem:** Need to validate if master data exists before replay, and create it if missing.

**MCP Solution:**

**Tool:** `erp_customer_exists`

```json
{
  "tool": "erp_customer_exists",
  "parameters": { "customer_id": "CUS-12345" }
}
// Response: { "exists": false }
```

**Tool:** `erp_create_customer`

```json
{
  "tool": "erp_create_customer",
  "parameters": {
    "customer_id": "CUS-12345",
    "name": "Acme Corp",
    "email": "contact@acme.com",
    "source": "autoheal"
  }
}
// Response: { "success": true, "entity_id": "CUS-12345" }
```

**Integration Point:** Repair Agent uses MCP tools to interact with ERP instead of direct SDK calls, enabling easier swapping of ERP systems (Dynamics 365, SAP, etc.).

---

## MCP Server Architecture for Continuum-Ops

### Recommended MCP Servers

| MCP Server | Tools | Resources | Hosting |
|------------|-------|-----------|---------|
| **azure-servicebus-mcp** | list_entities, peek_dlq_message, get_metrics, replay_message | Service Bus namespaces, queues, topics | Function App or Container |
| **azure-insights-mcp** | query_traces, query_exceptions, query_dependencies | Application Insights workspaces | Function App |
| **azure-cosmosdb-mcp** | query_patterns, get_incident_history | Cosmos DB containers | Function App (or direct SDK) |
| **erp-dynamics-mcp** | get_customer, create_customer, get_order | Dynamics 365 entities | Function App or Logic App |
| **erp-sap-mcp** | check_material, create_idoc | SAP systems | Separate service (on-prem connector) |

---

## MCP Server Deployment Options

### Option A: Embedded MCP Server (Recommended for MVP)

**Architecture:**
- MCP tools implemented as HTTP-triggered Azure Functions in the same Function App
- Co-located with agent modules
- Share managed identity and RBAC

**Example:**

```csharp
// Function: MCPToolServiceBusPeek
[Function("MCPTool_ServiceBusPeek")]
public async Task<HttpResponseData> ServiceBusPeek(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
{
    var parameters = await req.ReadFromJsonAsync<ServiceBusPeekParameters>();
    
    // Use managed identity to connect
    var client = new ServiceBusClient(parameters.Namespace, new DefaultAzureCredential());
    var receiver = client.CreateReceiver(parameters.EntityPath, new ServiceBusReceiverOptions 
    { 
        SubQueue = SubQueue.DeadLetter,
        ReceiveMode = ServiceBusReceiveMode.PeekLock 
    });
    
    var message = await receiver.PeekMessageAsync();
    
    return new McpToolResponse 
    {
        MessageId = message.MessageId,
        Body = message.Body.ToString(),
        Headers = message.ApplicationProperties
    };
}
```

**Pros:**
- Simplest deployment (single codebase)
- Low latency (in-process or same network)
- Shared infrastructure

**Cons:**
- Tight coupling (changes to MCP require Function App redeployment)
- Less reusable across other projects

---

### Option B: Standalone MCP Server (Recommended for Scale)

**Architecture:**
- Dedicated Function App or Azure Container App hosting MCP servers
- Exposed as HTTP API (MCP protocol over HTTP)
- Used by multiple agents or even other systems

**Example Deployment:**

```
Function App: func-mcp-servers-prod
  - HTTP Trigger: /mcp/azure-servicebus (handles all Service Bus tools)
  - HTTP Trigger: /mcp/azure-insights (handles all insights tools)
  - HTTP Trigger: /mcp/erp-dynamics (handles all Dynamics 365 tools)
```

**MCP Client in Diagnosis Agent:**

```csharp
var mcpClient = new HttpClient { BaseAddress = new Uri("https://func-mcp-servers-prod.azurewebsites.net") };

var toolRequest = new McpToolRequest
{
    Tool = "azure_servicebus_peek_dlq_message",
    Parameters = new { Namespace = "contoso-sb", EntityPath = "orders-queue" }
};

var response = await mcpClient.PostAsJsonAsync("/mcp/azure-servicebus", toolRequest);
var result = await response.Content.ReadFromJsonAsync<McpToolResponse>();
```

**Pros:**
- Reusable across projects
- Independent versioning and scaling
- Clear separation of concerns

**Cons:**
- Additional network hop (latency)
- More infrastructure to manage

---

### Option C: Hybrid Approach (Recommended)

**Embedded MCP Tools for Performance-Critical:**
- Service Bus peek/replay (low latency required)
- Cosmos DB queries (direct SDK faster)

**Standalone MCP Servers for External Systems:**
- ERP/Dynamics 365 (may change, reusable)
- SAP (on-prem connector needed)
- Custom business APIs

---

## MCP Tool Catalog for Continuum-Ops

### Azure Service Bus MCP Tools

| Tool Name | Parameters | Returns | Purpose |
|-----------|------------|---------|---------|
| `list_namespaces` | subscription_ids, tag_filter | namespaces[] | Discover Service Bus resources |
| `list_entities` | namespace | queues[], topics[] | List all queues/topics |
| `get_queue_metrics` | namespace, queue_name | active_count, dlq_count, size | Get runtime metrics |
| `peek_dlq_message` | namespace, entity_path, message_id | message_body, headers | Inspect DLQ message (read-only) |
| `replay_message` | namespace, entity_path, message_id | success | Move DLQ message to active |
| `move_to_quarantine` | namespace, entity_path, message_id, quarantine_queue | success | Isolate poison message |

### Azure Insights MCP Tools

| Tool Name | Parameters | Returns | Purpose |
|-----------|------------|---------|---------|
| `query_traces` | workspace_id, correlation_id, timespan | traces[] | Get correlated traces |
| `query_exceptions` | workspace_id, correlation_id, timespan | exceptions[] | Get correlated exceptions |
| `query_dependencies` | workspace_id, correlation_id, timespan | dependencies[] | Get dependency calls |
| `get_operation_timeline` | workspace_id, operation_id | timeline[] | Full operation flow |

### ERP (Dynamics 365) MCP Tools

| Tool Name | Parameters | Returns | Purpose |
|-----------|------------|---------|---------|
| `get_customer` | customer_id | customer_entity | Retrieve customer details |
| `create_customer` | customer_data | entity_id | Create missing customer |
| `get_order` | order_id | order_entity | Retrieve order details |
| `validate_master_data` | entity_type, entity_id | exists, valid | Pre-flight check |

### Cosmos DB Pattern MCP Tools

| Tool Name | Parameters | Returns | Purpose |
|-----------|------------|---------|---------|
| `find_similar_incidents` | error_signature, integration_id | incidents[] | Pattern matching |
| `get_failure_pattern` | signature | pattern_details | Retrieve known pattern |
| `update_pattern_stats` | pattern_id, outcome | success | Learning feedback |

---

## MCP Server Implementation Guide

### 1. Define MCP Server Manifest

Create a JSON manifest describing your MCP server:

```json
{
  "name": "azure-servicebus-mcp",
  "version": "1.0.0",
  "description": "MCP server for Azure Service Bus operations",
  "tools": [
    {
      "name": "list_entities",
      "description": "List all queues and topics in a Service Bus namespace",
      "inputSchema": {
        "type": "object",
        "properties": {
          "namespace": {
            "type": "string",
            "description": "Service Bus namespace FQDN"
          }
        },
        "required": ["namespace"]
      }
    },
    {
      "name": "peek_dlq_message",
      "description": "Peek a message from the dead-letter queue",
      "inputSchema": {
        "type": "object",
        "properties": {
          "namespace": { "type": "string" },
          "entity_path": { "type": "string" },
          "message_id": { "type": "string" }
        },
        "required": ["namespace", "entity_path"]
      }
    }
  ],
  "authentication": {
    "type": "azure_managed_identity"
  }
}
```

### 2. Implement MCP Server HTTP Handler

```csharp
[Function("MCPServerServiceBus")]
public async Task<HttpResponseData> HandleMcpRequest(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mcp/servicebus")] HttpRequestData req)
{
    var mcpRequest = await req.ReadFromJsonAsync<McpRequest>();
    
    return mcpRequest.Tool switch
    {
        "list_entities" => await HandleListEntities(mcpRequest.Parameters),
        "peek_dlq_message" => await HandlePeekDlqMessage(mcpRequest.Parameters),
        "replay_message" => await HandleReplayMessage(mcpRequest.Parameters),
        _ => CreateErrorResponse(req, "Unknown tool")
    };
}

private async Task<HttpResponseData> HandlePeekDlqMessage(JsonElement parameters)
{
    var @namespace = parameters.GetProperty("namespace").GetString();
    var entityPath = parameters.GetProperty("entity_path").GetString();
    
    var client = new ServiceBusClient(@namespace, new DefaultAzureCredential());
    var receiver = client.CreateReceiver(entityPath, new ServiceBusReceiverOptions 
    { 
        SubQueue = SubQueue.DeadLetter 
    });
    
    var message = await receiver.PeekMessageAsync();
    
    return CreateSuccessResponse(new 
    {
        message_id = message.MessageId,
        body = message.Body.ToString(),
        headers = message.ApplicationProperties,
        dead_letter_reason = message.DeadLetterReason
    });
}
```

### 3. Register MCP Tools with Azure OpenAI

In the Diagnosis Agent, convert MCP tool definitions to OpenAI function schemas:

```csharp
var mcpManifest = await LoadMcpManifest("azure-servicebus-mcp");

var openAiFunctions = mcpManifest.Tools.Select(tool => new ChatCompletionsFunctionToolDefinition
{
    Name = tool.Name,
    Description = tool.Description,
    Parameters = BinaryData.FromString(JsonSerializer.Serialize(tool.InputSchema))
}).ToList();

var chatOptions = new ChatCompletionsOptions
{
    DeploymentName = "gpt-4",
    Messages = { systemMessage, userMessage },
    Tools = { openAiFunctions }
};

var response = await openAiClient.GetChatCompletionsAsync(chatOptions);
```

### 4. Execute MCP Tool Calls

When OpenAI returns a tool call, invoke the MCP server:

```csharp
if (response.Value.Choices[0].FinishReason == CompletionsFinishReason.ToolCalls)
{
    foreach (var toolCall in response.Value.Choices[0].Message.ToolCalls)
    {
        var toolResult = await mcpClient.InvokeToolAsync(
            serverName: "azure-servicebus-mcp",
            toolName: toolCall.Name,
            parameters: toolCall.Arguments
        );
        
        // Send result back to OpenAI for next iteration
        messages.Add(new ChatRequestToolMessage(toolResult, toolCall.Id));
    }
    
    // Continue conversation with tool results
    var followUpResponse = await openAiClient.GetChatCompletionsAsync(chatOptions);
}
```

---

## MCP Server Security

### Authentication

**Option 1: Managed Identity (Recommended)**
- MCP server uses system-assigned managed identity
- Function-level authentication (Azure AD token required)
- No secrets in code

**Option 2: API Keys**
- Function App provides API key in headers
- MCP server validates key via Azure Functions built-in auth

### Authorization

- Implement RBAC at MCP tool level (e.g., only certain integrations can call `replay_message`)
- Log all tool invocations with caller identity
- Rate limiting per caller

### Audit Logging

```csharp
_logger.LogInformation("MCP Tool Invoked: {Tool} by {Caller} for {Integration}",
    toolName,
    callerIdentity,
    integrationId);
```

Store in Application Insights custom dimensions for audit trail.

---

## MCP Server Performance Optimization

### Caching

Cache expensive operations:
- Service Bus entity list (5 min TTL)
- Cosmos DB patterns (15 min TTL)
- ERP master data lookups (10 min TTL)

```csharp
private static readonly MemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

public async Task<List<Queue>> ListQueues(string @namespace)
{
    var cacheKey = $"sb-queues:{@namespace}";
    if (_cache.TryGetValue(cacheKey, out List<Queue> cached))
        return cached;
    
    var queues = await FetchQueuesFromAzure(@namespace);
    _cache.Set(cacheKey, queues, TimeSpan.FromMinutes(5));
    return queues;
}
```

### Connection Pooling

Reuse Service Bus clients, HTTP clients, and database connections:

```csharp
private static readonly Dictionary<string, ServiceBusClient> _sbClients = new();

public ServiceBusClient GetServiceBusClient(string @namespace)
{
    if (!_sbClients.ContainsKey(@namespace))
    {
        _sbClients[@namespace] = new ServiceBusClient(@namespace, new DefaultAzureCredential());
    }
    return _sbClients[@namespace];
}
```

---

## MCP vs. Direct SDK: Decision Matrix

| Scenario | Recommendation | Reasoning |
|----------|----------------|-----------|
| Service Bus message replay | **Embedded MCP or Direct SDK** | Low latency critical |
| Cosmos DB pattern queries | **Direct SDK** | In-process fastest |
| Application Insights queries | **MCP** | Complex Kusto queries, reusable |
| ERP API calls | **MCP** | Decouples from ERP SDK changes |
| Resource discovery | **MCP** | Standardized schema, AI-friendly |
| Circuit breaker logic | **Direct SDK** | Tight control needed |

**Rule of Thumb:**
- **Direct SDK**: Performance-critical, stable APIs, tight coupling acceptable
- **MCP**: Composability, AI-driven, cross-system integration, frequent changes

---

## Testing MCP Servers

### Unit Tests

Mock MCP server responses:

```csharp
[Fact]
public async Task DiagnosisAgent_UsesMcpToQueryInsights()
{
    var mockMcpClient = new Mock<IMcpClient>();
    mockMcpClient
        .Setup(m => m.InvokeToolAsync("azure-insights-mcp", "query_exceptions", It.IsAny<string>()))
        .ReturnsAsync(new McpToolResponse { Result = MockExceptionData });
    
    var diagnosisAgent = new DiagnosisAgent(mockMcpClient.Object);
    var result = await diagnosisAgent.DiagnoseAsync(incidentContext);
    
    Assert.Contains("Customer not found", result.RootCause);
}
```

### Integration Tests

Test against real MCP server in dev environment:

```csharp
[Fact]
public async Task McpServer_ServiceBus_PeekDlqMessage()
{
    var mcpClient = new HttpMcpClient("https://func-mcp-dev.azurewebsites.net");
    
    var result = await mcpClient.InvokeToolAsync(
        "azure-servicebus-mcp",
        "peek_dlq_message",
        new { Namespace = "test-sb.servicebus.windows.net", EntityPath = "test-queue" }
    );
    
    Assert.NotNull(result.MessageId);
}
```

---

## Future Enhancements

### Phase 2: MCP Server Marketplace

- Build library of reusable MCP servers for common integrations
- Publish as NuGet packages or container images
- Community-contributed MCP servers for SAP, Salesforce, etc.

### Phase 3: MCP-Based Workflows

- Use MCP servers to define entire remediation workflows
- AI generates workflow by composing MCP tools
- Store workflows as runbooks in Cosmos DB

### Phase 4: Cross-Product MCP Integration

- Integrate with Power Automate via MCP connectors
- Expose Continuum-Ops itself as an MCP server for other agents
- MCP-based observability for non-Azure systems (AWS, GCP)

---

## Summary

MCP servers in Continuum-Ops provide:

✅ **Standardized tool interface** for AI agents  
✅ **Composable architecture** across Azure and external systems  
✅ **Decoupling** from SDK changes and system migrations  
✅ **Enhanced AI capabilities** via dynamic tool selection  
✅ **Reusability** across projects and teams  

**Recommendation for MVP**: Start with embedded MCP tools for Azure resources, expand to standalone MCP servers for ERP integrations in Phase 2.
