using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Continuum.Ops.Functions.Mcp;

/// <summary>
/// Minimal JSON-RPC client for calling a single tool on a remote MCP server
/// (Streamable HTTP transport) directly, bypassing an LLM. Used only by the
/// deterministic Repair Agent, which decides which tool to call itself
/// rather than delegating that decision to a model.
///
/// The Diagnosis and Verify Prompt Agents do NOT use this client — they
/// call MCP tools themselves through Foundry's Toolbox.
/// </summary>
public interface IMcpToolClient
{
    Task<JsonElement> CallToolAsync(string toolName, object arguments, CancellationToken ct = default);
}

public class McpToolClient : IMcpToolClient
{
    private readonly HttpClient _http;
    private int _requestId;

    public McpToolClient(HttpClient http)
    {
        _http = http;
        var baseUrl = Environment.GetEnvironmentVariable("MCP_SERVER_BASE_URL")
            ?? throw new InvalidOperationException("MCP_SERVER_BASE_URL app setting is not configured");
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    }

    public async Task<JsonElement> CallToolAsync(string toolName, object arguments, CancellationToken ct = default)
    {
        // Function system key ("mcp_extension") for the default "System" webhookAuthorizationLevel.
        var systemKey = Environment.GetEnvironmentVariable("MCP_SERVER_SYSTEM_KEY");
        var path = "runtime/webhooks/mcp" + (systemKey is null ? "" : $"?code={Uri.EscapeDataString(systemKey)}");

        var payload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = ++_requestId,
            ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = toolName,
                ["arguments"] = JsonSerializer.SerializeToNode(arguments),
            },
        };

        var response = await _http.PostAsJsonAsync(path, payload, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (doc.RootElement.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException($"MCP tool '{toolName}' failed: {error.GetRawText()}");
        }

        return doc.RootElement.GetProperty("result").Clone();
    }
}
