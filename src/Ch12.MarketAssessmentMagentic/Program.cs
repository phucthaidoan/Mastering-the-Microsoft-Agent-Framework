using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

#pragma warning disable MAAIW001

EnvLoader.LoadRepoRootEnv();

IChatClient chatClient = AgentClientFactory.CreateChatClient();

const string launchScenario = """
A small regional retailer is considering a 60-day pilot for a same-day pickup
service for household essentials. The pilot would run in two urban stores, use
existing inventory systems, and require a dedicated staging shelf near the front
of each store.

Create a coordinated market assessment and recommend whether the pilot should move forward.
""";

AIAgent managerAgent = chatClient.AsAIAgent(
    instructions: """
    You are AssessmentManager.
    Coordinate the analysts to produce a practical recommendation.
    Ask the best specialist to contribute when needed, track progress, and stop when the assessment is complete.
    Keep the final answer under 180 words.
    """,
    name: "AssessmentManager");

AIAgent marketAnalyst = chatClient.AsAIAgent(
    instructions: """
    You are MarketAnalyst.
    Evaluate customer demand, positioning, adoption barriers, and launch messaging.
    Stay grounded in the provided scenario.
    Keep your response to three concise bullets.
    """,
    name: "MarketAnalyst");

AIAgent operationsAnalyst = chatClient.AsAIAgent(
    instructions: """
    You are OperationsAnalyst.
    Evaluate staffing, store flow, staging, inventory handling, and operational constraints.
    Stay grounded in the provided scenario.
    Keep your response to three concise bullets.
    """,
    name: "OperationsAnalyst");

AIAgent financeAnalyst = chatClient.AsAIAgent(
    instructions: """
    You are FinanceAnalyst.
    Evaluate cost exposure, measurable outcomes, financial guardrails, and rollout criteria.
    Stay grounded in the provided scenario.
    Keep your response to three concise bullets.
    """,
    name: "FinanceAnalyst");

Workflow workflow = new MagenticWorkflowBuilder(managerAgent)
    .AddParticipants([marketAnalyst, operationsAnalyst, financeAnalyst])
    .WithName("MarketAssessmentMagentic")
    .WithDescription("Coordinates a market assessment across specialist agents.")
    .WithMaxRounds(2)
    .RequirePlanSignoff(false)
    .Build();

AIAgent magenticAgent = workflow.AsAIAgent(
    id: "MarketAssessmentMagentic",
    name: "MarketAssessmentMagentic",
    description: "Runs a Magentic-style market assessment workflow.",
    executionEnvironment: InProcessExecution.Concurrent,
    includeExceptionDetails: true,
    includeWorkflowOutputsInResponse: true);

Console.WriteLine("===== Market assessment request =====");
Console.WriteLine(launchScenario);
Console.WriteLine();

AgentResponse response = await magenticAgent.RunAsync(launchScenario);

Console.WriteLine("===== Final Magentic assessment =====");
Console.WriteLine(GetFinalAssistantText(response));

static string GetFinalAssistantText(AgentResponse response)
{
    return response.Messages
        .LastOrDefault(message => message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text))
        ?.Text
        .Trim()
        ?? response.Text;
}
