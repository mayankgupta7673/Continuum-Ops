using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;

namespace Continuum.Ops.Functions.Agents;

/// <summary>
/// Thin HTTP client for the Microsoft Foundry Agent Service Responses API
/// (single entry point for both Prompt Agents and raw model calls).
///
/// NOTE: this calls the Responses API with an inline model + instructions
/// payload, which works today against any Foundry OpenAI-compatible
/// deployment. If your Foundry SDK version supports invoking a *named,
/// persisted* Prompt Agent by ID directly (rather than resending
/// instructions on every call), check the current azure-ai-projects /
/// OpenAI Responses client docs for the exact request shape (e.g. an
/// `agent` or `assistant_id`-style field) and swap it in here — the
/// call site (AgentActivities) does not need to change.
/// </summary>
public interface IFoundryAgentClient
{
    Task<string> InvokeAsync(string agentId, string instructions, string modelDeploymentName, string userInput, CancellationToken ct = default);
}

public class FoundryAgentClient : IFoundryAgentClient
{
    private readonly HttpClient _http;
    private readonly TokenCredential _credential = new DefaultAzureCredential();
    private static readonly string[] Scopes = { "https://ai.azure.com/.default" };

    public FoundryAgentClient(HttpClient http)
    {
        _http = http;
        var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
            ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT app setting is not configured");
        _http.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
    }

    public async Task<string> InvokeAsync(string agentId, string instructions, string modelDeploymentName, string userInput, CancellationToken ct = default)
    {
        var token = await _credential.GetTokenAsync(new TokenRequestContext(Scopes), ct);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var apiVersion = Environment.GetEnvironmentVariable("FOUNDRY_API_VERSION") ?? "preview";
        var request = new
        {
            model = modelDeploymentName,
            instructions,
            input = userInput,
            metadata = new { agentId }, // carried through for tracing/correlation
        };

        var response = await _http.PostAsJsonAsync($"openai/v1/responses?api-version={apiVersion}", request, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        // Responses API returns an `output` array; extract the first text content item.
        var outputText = doc.RootElement
            .GetProperty("output")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return outputText ?? throw new InvalidOperationException("Foundry Responses API returned no output text");
    }
}
