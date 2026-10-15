using System.ComponentModel;
using System.Diagnostics;
using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string SourceName = "MasteringAgentFramework.Ch14";

EnvLoader.LoadRepoRootEnv();

string? appInsightsConnectionString =
    Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

TracerProviderBuilder tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Ch14.Logistics"))
    .AddSource(SourceName)
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddSource("Experimental.Microsoft.Extensions.AI")
    .AddSource("*Microsoft.Extensions.AI*")
    .AddConsoleExporter();

if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    tracerProviderBuilder.AddAzureMonitorTraceExporter(options =>
        options.ConnectionString = appInsightsConnectionString);
}

using TracerProvider tracerProvider = tracerProviderBuilder.Build();

IChatClient chatClient = AgentClientFactory.CreateChatClient();

AIAgent agent = chatClient.AsAIAgent(
        instructions: """
        You are a logistics-routing assistant.
        Use the LookUpShipment tool before answering shipment route questions.
        Recommend one route from the candidate routes in the shipment record.
        Explain the reason briefly using only the shipment data.
        If a request does not need shipment data, answer briefly without using the tool.
        """,
        name: "LogisticsRouter",
        tools: [AIFunctionFactory.Create(ShipmentTools.LookUpShipment)])
    .AsBuilder()
    .UseOpenTelemetry(sourceName: SourceName)
    .Build();

string mode = args.Length > 0 ? args[0] : "scripted";

if (string.Equals(mode, "interactive", StringComparison.OrdinalIgnoreCase))
{
    await RunInteractiveAsync(agent);
}
else
{
    await RunScriptedAsync(agent);
}

static async Task RunScriptedAsync(AIAgent agent)
{
    const string request = "Look up shipment SHP-4021 and recommend the best route.";

    Console.WriteLine("===== Scripted logistics request =====");
    Console.WriteLine($"User: {request}");
    Console.WriteLine();

    AgentResponse response = await agent.RunAsync(request);

    Console.WriteLine("===== Response =====");
    Console.WriteLine(response.Text);
    Console.WriteLine();
}

static async Task RunInteractiveAsync(AIAgent agent)
{
    Console.WriteLine("Interactive logistics observability. Tool-using requests emit richer traces. Empty line or exit stops.");

    while (true)
    {
        Console.Write("You: ");
        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input) ||
            string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        AgentResponse response = await agent.RunAsync(input);
        Console.WriteLine("Response:");
        Console.WriteLine(response.Text);
        Console.WriteLine();
    }
}

internal static class ShipmentTools
{
    private static readonly ActivitySource _activitySource = new("MasteringAgentFramework.Ch14");

    private static readonly IReadOnlyDictionary<string, ShipmentRecord> _shipments =
        new Dictionary<string, ShipmentRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["SHP-4021"] = new(
                "SHP-4021",
                "North Dock",
                "Harbor West",
                1180,
                [
                    new RouteOption("RTE-7A", "rail to inland hub, then short-haul truck", 2, 1640, "lowest emissions and reliable schedule"),
                    new RouteOption("RTE-9C", "direct truck corridor", 1, 2310, "fastest but higher cost"),
                    new RouteOption("RTE-4B", "coastal feeder, then truck transfer", 3, 1880, "moderate cost with weather exposure")
                ]),
            ["SHP-5188"] = new(
                "SHP-5188",
                "Valley Crossdock",
                "Metro South",
                640,
                [
                    new RouteOption("RTE-2D", "overnight truck lane", 1, 980, "fastest and under weight threshold"),
                    new RouteOption("RTE-3F", "shared regional truck", 2, 740, "lower cost but later arrival")
                ]),
            ["SHP-7730"] = new(
                "SHP-7730",
                "Lakeside Depot",
                "Desert Junction",
                2140,
                [
                    new RouteOption("RTE-6K", "rail intermodal transfer", 3, 2050, "best cost for heavier freight"),
                    new RouteOption("RTE-8M", "team-driver truck route", 2, 2940, "faster but expensive")
                ])
        };

    [Description("Looks up invented shipment details and candidate route options by shipment id.")]
    public static string LookUpShipment(
        [Description("The shipment id to look up, such as SHP-4021.")]
        string shipmentId)
    {
        using Activity? activity = _activitySource.StartActivity("execute_tool LookUpShipment", ActivityKind.Internal);
        activity?.SetTag("gen_ai.operation.name", "execute_tool");
        activity?.SetTag("gen_ai.tool.name", nameof(LookUpShipment));
        activity?.SetTag("logistics.shipment.id", shipmentId);

        if (!_shipments.TryGetValue(shipmentId.Trim(), out ShipmentRecord? shipment))
        {
            return $"Shipment {shipmentId} was not found.";
        }

        string routes = string.Join(
            Environment.NewLine,
            shipment.Routes.Select(route =>
                $"- {route.Id}: {route.Description}; {route.Days} day(s); {route.Cost:F0} dollars; note: {route.Note}"));

        return $"""
        Shipment id: {shipment.Id}
        Origin: {shipment.Origin}
        Destination: {shipment.Destination}
        Weight: {shipment.WeightPounds} lb
        Candidate routes:
        {routes}
        """;
    }

    private sealed record ShipmentRecord(
        string Id,
        string Origin,
        string Destination,
        int WeightPounds,
        IReadOnlyList<RouteOption> Routes);

    private sealed record RouteOption(
        string Id,
        string Description,
        int Days,
        decimal Cost,
        string Note);
}
