#pragma warning disable AIEVAL001

using System.ComponentModel;
using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.NLP;
using Microsoft.Extensions.AI.Evaluation.Quality;

namespace MasteringAgentFramework.Ch15.LogisticsEvaluation;

[TestClass]
public sealed class LogisticsEvaluationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DeterministicF1EvaluatorScoresReferenceAnswer()
    {
        const string responseText = """
        Recommend RTE-7A for shipment SHP-4021 because it uses rail to an inland hub and short-haul truck, costs 1640 dollars, takes 2 days, and has the lowest emissions with a reliable schedule.
        """;

        const string referenceAnswer = """
        Recommend RTE-7A for shipment SHP-4021 because it costs 1640 dollars, takes 2 days, and has the lowest emissions with a reliable schedule.
        """;

        ChatMessage request = new(ChatRole.User, "Look up shipment SHP-4021 and recommend the best route.");
        ChatResponse response = new(new ChatMessage(ChatRole.Assistant, responseText));

        F1Evaluator evaluator = new();
        EvaluationResult result = await evaluator.EvaluateAsync(
            [request],
            response,
            chatConfiguration: null,
            additionalContext: [new F1EvaluatorContext(referenceAnswer)]);

        NumericMetric metric = result.Get<NumericMetric>(F1Evaluator.F1MetricName);

        WriteLine($"Deterministic F1 score: {metric.Value:F3}");
        WriteLine($"F1 interpretation rating: {metric.Interpretation?.Rating}");
        WriteLine($"F1 interpretation failed: {metric.Interpretation?.Failed}");

        Assert.IsTrue(metric.Value >= 0.80, $"Expected F1 >= 0.80 but got {metric.Value:F3}.");
    }

    [TestMethod]
    public async Task ToolCallAccuracyEvaluatorScoresShipmentLookupToolUse()
    {
        EnvLoader.LoadRepoRootEnv();

        IChatClient chatClient = AgentClientFactory.CreateChatClient();
        AITool[] tools = [AIFunctionFactory.Create(ShipmentTools.LookUpShipment)];

        AIAgent agent = chatClient.AsAIAgent(
            instructions: """
            You are a logistics-routing assistant.
            Use the LookUpShipment tool before answering shipment route questions.
            Recommend one route from the candidate routes in the shipment record.
            Explain the reason briefly using only the shipment data.
            """,
            name: "LogisticsRouter",
            tools: tools);

        const string requestText = "Look up shipment SHP-4021 and recommend the best route.";
        ChatMessage request = new(ChatRole.User, requestText);
        AgentResponse agentResponse = await agent.RunAsync(request);

        ChatResponse response = agentResponse.AsChatResponse();
        IChatClient judgeChatClient = AgentClientFactory.CreateChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        ChatConfiguration chatConfiguration = new(judgeChatClient);
        ToolCallAccuracyEvaluator evaluator = new();
        EvaluationResult result = await evaluator.EvaluateAsync(
            [request],
            response,
            chatConfiguration,
            [new ToolCallAccuracyEvaluatorContext(tools)]);

        BooleanMetric metric = result.Get<BooleanMetric>(ToolCallAccuracyEvaluator.ToolCallAccuracyMetricName);

        WriteLine($"Logistics agent response: {agentResponse.Text}");
        WriteLine($"Tool call accuracy value: {metric.Value}");
        WriteLine($"Tool call accuracy rating: {metric.Interpretation?.Rating}");
        WriteLine($"Tool call accuracy failed: {metric.Interpretation?.Failed}");
        WriteLine($"Tool call accuracy reason: {metric.Interpretation?.Reason ?? metric.Reason}");

        Assert.IsFalse(metric.Interpretation?.Failed == true, "The LLM judge marked tool-call accuracy as failed.");
    }

    private void WriteLine(string message)
    {
        TestContext.WriteLine(message);
        Console.WriteLine(message);
    }
}

internal static class ShipmentTools
{
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

    [System.ComponentModel.Description("Looks up invented shipment details and candidate route options by shipment id.")]
    public static string LookUpShipment(
        [System.ComponentModel.Description("The shipment id to look up, such as SHP-4021.")]
        string shipmentId)
    {
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
