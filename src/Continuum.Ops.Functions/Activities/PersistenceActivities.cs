using System.Net;
using Continuum.Ops.Functions.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Azure.Identity;

namespace Continuum.Ops.Functions.Activities;

/// <summary>Persists incident state to Cosmos DB for audit trail and history.</summary>
public class PersistenceActivities
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<PersistenceActivities> _logger;

    public PersistenceActivities(ILogger<PersistenceActivities> logger)
    {
        _logger = logger;
        var endpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")
            ?? throw new InvalidOperationException("COSMOS_ENDPOINT app setting is not configured");
        _cosmosClient = new CosmosClient(endpoint, new DefaultAzureCredential());
    }

    [Function(nameof(SaveIncidentRecord))]
    public async Task SaveIncidentRecord([ActivityTrigger] IncidentRecord record)
    {
        var databaseName = Environment.GetEnvironmentVariable("COSMOS_DATABASE_NAME") ?? "ContinuumOps";
        var containerName = Environment.GetEnvironmentVariable("COSMOS_INCIDENTS_CONTAINER") ?? "Incidents";
        var container = _cosmosClient.GetContainer(databaseName, containerName);

        _logger.LogInformation("Saving incident {IncidentId} with status {Status}", record.Id, record.Status);
        await container.UpsertItemAsync(record, new PartitionKey(record.TenantId));
    }
}
