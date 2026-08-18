using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Continuum.Ops.Functions.Triggers;

/// <summary>
/// Approve/reject callback links clicked from the Teams Adaptive Card.
/// Raises a Durable Functions external event to resume the waiting orchestration.
/// </summary>
public class ApprovalCallbackTrigger
{
    private readonly ILogger<ApprovalCallbackTrigger> _logger;

    public ApprovalCallbackTrigger(ILogger<ApprovalCallbackTrigger> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ApproveIncident))]
    public async Task<HttpResponseData> ApproveIncident(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "incidents/{instanceId}/approve")] HttpRequestData req,
        string instanceId,
        [DurableClient] DurableTaskClient durableClient)
    {
        return await RaiseApprovalEvent(req, durableClient, instanceId, approved: true);
    }

    [Function(nameof(RejectIncident))]
    public async Task<HttpResponseData> RejectIncident(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "incidents/{instanceId}/reject")] HttpRequestData req,
        string instanceId,
        [DurableClient] DurableTaskClient durableClient)
    {
        return await RaiseApprovalEvent(req, durableClient, instanceId, approved: false);
    }

    private async Task<HttpResponseData> RaiseApprovalEvent(
        HttpRequestData req, DurableTaskClient durableClient, string instanceId, bool approved)
    {
        _logger.LogInformation("Approval callback for {InstanceId}: approved={Approved}", instanceId, approved);

        await durableClient.RaiseEventAsync(instanceId, "ApprovalReceived", approved);

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteStringAsync(approved
            ? $"Repair plan for incident {instanceId} approved. Continuum-Ops will proceed."
            : $"Repair plan for incident {instanceId} rejected. No repair action will be taken.");
        return response;
    }
}
