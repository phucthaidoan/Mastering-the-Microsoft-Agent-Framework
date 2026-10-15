#pragma warning disable MEAI001

using A2A;
using A2A.AspNetCore;
using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.A2A;
using Microsoft.Extensions.AI;

const string serverUrl = "http://localhost:5240";
const string a2aPath = "/a2a/route-advisor";

EnvLoader.LoadRepoRootEnv();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(serverUrl);

IChatClient chatClient = AgentClientFactory.CreateChatClient();

AIAgent routeAdvisor = chatClient.AsAIAgent(
    instructions: """
    You are RouteAdvisor, a remote logistics route-advisory specialist.
    Given a shipment description, provide brief routing guidance using plain language.
    Consider urgency, distance, handoff risk, cost sensitivity, and freight constraints.
    Keep answers concise and do not invent tracking records or operational facts.
    """,
    name: "RouteAdvisor",
    description: "Gives brief route-advisory guidance for invented logistics shipment scenarios.");

builder.AddA2AServer(
    routeAdvisor,
    options => options.AgentRunMode = AgentRunMode.DisallowBackground);

WebApplication app = builder.Build();

AgentCard agentCard = new()
{
    Name = "RouteAdvisor",
    Description = "A remote logistics specialist that gives brief route-advisory guidance.",
    Version = "1.0.0",
    SupportedInterfaces =
    [
        new AgentInterface
        {
            Url = $"{serverUrl}{a2aPath}",
            ProtocolBinding = ProtocolBindingNames.HttpJson,
            ProtocolVersion = "1.0"
        }
    ],
    DefaultInputModes = ["text/plain"],
    DefaultOutputModes = ["text/plain"],
    Capabilities = new AgentCapabilities { Streaming = true },
    Skills =
    [
        new A2A.AgentSkill
        {
            Id = "route-advice",
            Name = "Route advice",
            Description = "Recommends a practical routing approach for a described shipment.",
            Tags = ["logistics", "routing"]
        }
    ]
};

app.MapA2AHttpJson(routeAdvisor, a2aPath);
app.MapWellKnownAgentCard(agentCard);

app.MapGet("/", () => "RouteAdvisor A2A server is running. Agent card: /.well-known/agent-card.json");

Console.WriteLine($"RouteAdvisor A2A server listening on {serverUrl}");
Console.WriteLine($"A2A endpoint: {serverUrl}{a2aPath}");
Console.WriteLine($"Agent card: {serverUrl}/.well-known/agent-card.json");

app.Run();
