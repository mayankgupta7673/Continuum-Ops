// Continuum-Ops core infrastructure.
// Deploys the shared platform resources: Cosmos DB, AI Search, Azure OpenAI,
// Application Insights/Log Analytics, storage, and the two Function Apps
// (Python MCP tool server, .NET orchestrator/Repair Agent). Managed identity
// only — no connection strings or API keys are provisioned here.
//
// The Foundry project itself and the Service Bus namespace(s) being
// monitored are NOT created by this template (Service Bus is typically an
// existing, possibly cross-subscription, resource — see
// docs/02-Deployment-Guide.md for the cross-subscription RBAC steps).
//
// Deploy:
//   az deployment group create -g <rg> --template-file main.bicep \
//     --parameters environmentName=dev location=eastus openAiDeploymentName=gpt-4o

@description('Environment identifier, used in resource names.')
@allowed(['dev', 'staging', 'prod'])
param environmentName string = 'dev'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Azure OpenAI model deployment name backing the Prompt Agents.')
param openAiDeploymentName string = 'gpt-4o'

@description('Function App Premium plan SKU.')
@allowed(['EP1', 'EP2'])
param functionAppSku string = 'EP1'

var suffix = '${environmentName}-${location}'
var tags = {
  application: 'continuum-ops'
  environment: environmentName
}

// ---------------------------------------------------------------------------
// Storage (required by both Function Apps)
// ---------------------------------------------------------------------------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: replace('stcontinuumops${suffix}', '-', '')
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

// ---------------------------------------------------------------------------
// Observability
// ---------------------------------------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'law-continuumops-${suffix}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-continuumops-${suffix}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ---------------------------------------------------------------------------
// Cosmos DB (incidents + patterns)
// ---------------------------------------------------------------------------
resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2024-08-15' = {
  name: 'cosmos-continuumops-${suffix}'
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [{ locationName: location, failoverPriority: 0 }]
    disableLocalAuth: true
    capabilities: environmentName == 'dev' ? [{ name: 'EnableServerless' }] : []
  }
}

resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-08-15' = {
  parent: cosmos
  name: 'continuumops'
  properties: {
    resource: { id: 'continuumops' }
  }
}

resource incidentsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-08-15' = {
  parent: cosmosDatabase
  name: 'incidents'
  properties: {
    resource: {
      id: 'incidents'
      partitionKey: { paths: ['/tenantId'], kind: 'Hash' }
    }
  }
}

resource patternsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-08-15' = {
  parent: cosmosDatabase
  name: 'patterns'
  properties: {
    resource: {
      id: 'patterns'
      partitionKey: { paths: ['/tenantId'], kind: 'Hash' }
    }
  }
}

// ---------------------------------------------------------------------------
// AI Search (pattern similarity search)
// ---------------------------------------------------------------------------
resource search 'Microsoft.Search/searchServices@2024-06-01-preview' = {
  name: 'srch-continuumops-${suffix}'
  location: location
  tags: tags
  sku: { name: 'standard' }
  properties: {
    disableLocalAuth: true
    replicaCount: 1
    partitionCount: 1
  }
}

// ---------------------------------------------------------------------------
// Azure OpenAI (backs the Foundry Prompt Agents)
// ---------------------------------------------------------------------------
resource openAi 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: 'aoai-continuumops-${suffix}'
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: { name: 'S0' }
  properties: {
    customSubDomainName: 'aoai-continuumops-${suffix}'
    disableLocalAuth: true
  }
}

resource openAiDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAi
  name: openAiDeploymentName
  sku: { name: 'Standard', capacity: 10 }
  properties: {
    model: { format: 'OpenAI', name: 'gpt-4o', version: '2024-08-06' }
  }
}

// ---------------------------------------------------------------------------
// Function Apps
// ---------------------------------------------------------------------------
resource functionPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-continuumops-${suffix}'
  location: location
  tags: tags
  sku: { name: functionAppSku, tier: 'ElasticPremium' }
  kind: 'elastic'
}

resource mcpServerApp 'Microsoft.Web/sites@2023-12-01' = {
  name: 'func-continuumops-mcp-${suffix}'
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: functionPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'PYTHON|3.11'
      appSettings: [
        { name: 'AzureWebJobsStorage__accountName', value: storage.name }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'python' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'SEARCH_ENDPOINT', value: 'https://${search.name}.search.windows.net' }
        { name: 'COSMOS_ENDPOINT', value: cosmos.properties.documentEndpoint }
        { name: 'COSMOS_DATABASE_NAME', value: cosmosDatabase.name }
        { name: 'COSMOS_PATTERNS_CONTAINER', value: patternsContainer.name }
      ]
    }
  }
}

resource orchestratorApp 'Microsoft.Web/sites@2023-12-01' = {
  name: 'func-continuumops-orchestrator-${suffix}'
  location: location
  tags: tags
  kind: 'functionapp'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: functionPlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        { name: 'AzureWebJobsStorage__accountName', value: storage.name }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'COSMOS_ENDPOINT', value: cosmos.properties.documentEndpoint }
        { name: 'COSMOS_DATABASE_NAME', value: cosmosDatabase.name }
        { name: 'COSMOS_INCIDENTS_CONTAINER', value: incidentsContainer.name }
        { name: 'MCP_SERVER_BASE_URL', value: 'https://${mcpServerApp.properties.defaultHostName}' }
      ]
    }
  }
}

// ---------------------------------------------------------------------------
// RBAC — least-privilege data-plane roles for the Function Apps' managed
// identities. Service Bus roles are assigned separately (see
// docs/02-Deployment-Guide.md) because the namespace is often cross-subscription.
// ---------------------------------------------------------------------------
var cosmosDataContributorRoleId = '00000000-0000-0000-0000-000000000002' // Cosmos DB Built-in Data Contributor
var searchIndexDataContributorRoleId = '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
var cognitiveServicesOpenAiUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'

resource mcpCosmosRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-08-15' = {
  parent: cosmos
  name: guid(cosmos.id, mcpServerApp.id, 'cosmos-data-contributor')
  properties: {
    roleDefinitionId: '${cosmos.id}/sqlRoleDefinitions/${cosmosDataContributorRoleId}'
    principalId: mcpServerApp.identity.principalId
    scope: cosmos.id
  }
}

resource orchestratorCosmosRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-08-15' = {
  parent: cosmos
  name: guid(cosmos.id, orchestratorApp.id, 'cosmos-data-contributor')
  properties: {
    roleDefinitionId: '${cosmos.id}/sqlRoleDefinitions/${cosmosDataContributorRoleId}'
    principalId: orchestratorApp.identity.principalId
    scope: cosmos.id
  }
}

resource mcpSearchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, mcpServerApp.id, searchIndexDataContributorRoleId)
  scope: search
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataContributorRoleId)
    principalId: mcpServerApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource orchestratorOpenAiRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAi.id, orchestratorApp.id, cognitiveServicesOpenAiUserRoleId)
  scope: openAi
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAiUserRoleId)
    principalId: orchestratorApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output mcpServerHostName string = mcpServerApp.properties.defaultHostName
output orchestratorHostName string = orchestratorApp.properties.defaultHostName
output cosmosEndpoint string = cosmos.properties.documentEndpoint
output searchEndpoint string = 'https://${search.name}.search.windows.net'
output openAiEndpoint string = openAi.properties.endpoint
