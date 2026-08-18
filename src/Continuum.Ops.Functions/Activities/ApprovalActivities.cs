using System.Net.Http.Json;
using Continuum.Ops.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Continuum.Ops.Functions.Activities;

/// <summary>Posts a Teams Adaptive Card asking a human to approve/reject the repair plan.</summary>
public class ApprovalActivities
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApprovalActivities> _logger;

    public ApprovalActivities(IHttpClientFactory httpClientFactory, ILogger<ApprovalActivities> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [Function(nameof(RequestApproval))]
    public async Task RequestApproval([ActivityTrigger] ApprovalRequest request)
    {
        var webhookUrl = Environment.GetEnvironmentVariable("TEAMS_APPROVAL_WEBHOOK_URL");
        if (string.IsNullOrEmpty(webhookUrl))
        {
            _logger.LogWarning("TEAMS_APPROVAL_WEBHOOK_URL not configured — skipping Teams notification for {AlertId}", request.Alert.AlertId);
            return;
        }

        var client = _httpClientFactory.CreateClient();
        var card = new
        {
            type = "AdaptiveCard",
            body = new object[]
            {
                new { type = "TextBlock", text = $"Continuum-Ops: Repair plan for {request.Alert.Queue}", weight = "Bolder", size = "Medium" },
                new { type = "TextBlock", text = $"Root cause: {request.Diagnosis.RootCause}", wrap = true },
                new { type = "TextBlock", text = $"Confidence: {request.Diagnosis.Confidence:P0} · Risk: {request.Diagnosis.RiskLevel}", wrap = true },
            },
            actions = new object[]
            {
                new { type = "Action.OpenUrl", title = "Approve", url = $"{request.ApprovalCallbackBaseUrl}/api/incidents/{request.InstanceId}/approve" },
                new { type = "Action.OpenUrl", title = "Reject", url = $"{request.ApprovalCallbackBaseUrl}/api/incidents/{request.InstanceId}/reject" },
            },
        };

        await client.PostAsJsonAsync(webhookUrl, new { type = "message", attachments = new[] { new { contentType = "application/vnd.microsoft.card.adaptive", content = card } } });
    }
}

public record ApprovalRequest(IncidentAlert Alert, DiagnosisResult Diagnosis, string InstanceId, string ApprovalCallbackBaseUrl);
