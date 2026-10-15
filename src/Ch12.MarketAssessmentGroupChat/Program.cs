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

Should the pilot move forward?
""";

AIAgent marketAnalyst = chatClient.AsAIAgent(
    instructions: """
    You are MarketAnalyst.
    Evaluate the product-launch scenario from the customer and market angle only.
    Mention demand signals, positioning, and adoption risks.
    Keep your contribution concise and grounded in the scenario.
    """,
    name: "MarketAnalyst");

AIAgent operationsAnalyst = chatClient.AsAIAgent(
    instructions: """
    You are OperationsAnalyst.
    Evaluate the product-launch scenario from the operational execution angle only.
    Mention staffing, store flow, inventory handling, and pilot constraints.
    Keep your contribution concise and grounded in the scenario.
    """,
    name: "OperationsAnalyst");

AIAgent financeAnalyst = chatClient.AsAIAgent(
    instructions: """
    You are FinanceAnalyst.
    Evaluate the product-launch scenario from the financial angle only.
    Mention cost exposure, measurable outcomes, and go/no-go guardrails.
    Keep your contribution concise and grounded in the scenario.
    """,
    name: "FinanceAnalyst");

Workflow workflow = AgentWorkflowBuilder
    .CreateGroupChatBuilderWith(agents =>
        new RoundRobinGroupChatManager(agents) { MaximumIterationCount = 3 })
    .AddParticipants([marketAnalyst, operationsAnalyst, financeAnalyst])
    .Build();

AIAgent groupChatAgent = workflow.AsAIAgent(
    id: "MarketAssessmentGroupChat",
    name: "MarketAssessmentGroupChat",
    description: "Runs a round-robin market assessment discussion.",
    executionEnvironment: InProcessExecution.Concurrent,
    includeExceptionDetails: true,
    includeWorkflowOutputsInResponse: true);

Console.WriteLine("===== Market assessment question =====");
Console.WriteLine(launchScenario);
Console.WriteLine();

AgentResponse response = await groupChatAgent.RunAsync(launchScenario);

Console.WriteLine("===== Agent contributions =====");
PrintAgentContributions(response);

Console.WriteLine("===== Final group-chat state =====");
Console.WriteLine("Completed one round with MarketAnalyst, OperationsAnalyst, and FinanceAnalyst.");

static void PrintAgentContributions(AgentResponse response)
{
    foreach (ChatMessage message in response.Messages)
    {
        if (message.Role != ChatRole.Assistant || string.IsNullOrWhiteSpace(message.AuthorName))
        {
            continue;
        }

        Console.WriteLine($"{message.AuthorName}:");
        Console.WriteLine(message.Text.Trim());
        Console.WriteLine();
    }
}
