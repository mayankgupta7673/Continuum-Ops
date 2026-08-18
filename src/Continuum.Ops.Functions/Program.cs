using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddHttpClient<Continuum.Ops.Functions.Agents.IFoundryAgentClient, Continuum.Ops.Functions.Agents.FoundryAgentClient>();
        services.AddHttpClient<Continuum.Ops.Functions.Mcp.IMcpToolClient, Continuum.Ops.Functions.Mcp.McpToolClient>();
    })
    .Build();

host.Run();
