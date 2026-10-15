using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

#pragma warning disable MAAIW001

EnvLoader.LoadRepoRootEnv();

IChatClient chatClient = AgentClientFactory.CreateChatClient();

AIAgent triage = chatClient.AsAIAgent(
    instructions: """
    You are ServiceDeskRouter, an IT service-desk router.
    Do not solve the request directly.
    Route network connectivity, Wi-Fi, VPN, or latency issues to NetworkSupport.
    Route sign-in, account, password, permissions, or access issues to AccessSupport.
    If the request is unclear, ask one brief clarifying question.
    """,
    name: "ServiceDeskRouter");

AIAgent networkSupport = chatClient.AsAIAgent(
    instructions: """
    You are NetworkSupport.
    Help with connectivity, Wi-Fi, VPN, DNS, and latency issues.
    Give concise, practical next steps and mention what evidence to collect.
    """,
    name: "NetworkSupport");

AIAgent accessSupport = chatClient.AsAIAgent(
    instructions: """
    You are AccessSupport.
    Help with sign-in, account, password, permission, and application access issues.
    Give concise, practical next steps and mention what identity or access detail to check.
    """,
    name: "AccessSupport");

Workflow workflow = AgentWorkflowBuilder
    .CreateHandoffBuilderWith(triage)
    .WithHandoffs(triage, [networkSupport, accessSupport])
    .WithHandoffs(networkSupport, [triage])
    .WithHandoffs(accessSupport, [triage])
    .EnableReturnToPrevious()
    .Build();

AIAgent routingAgent = workflow.AsAIAgent(
    id: "ItRoutingWorkflow",
    name: "ItRoutingWorkflow",
    description: "Routes IT service-desk requests to the right specialist.",
    executionEnvironment: InProcessExecution.Concurrent,
    includeExceptionDetails: true,
    includeWorkflowOutputsInResponse: true);

string mode = args.Length > 0 ? args[0] : "scripted";

if (string.Equals(mode, "interactive", StringComparison.OrdinalIgnoreCase))
{
    await RunInteractiveAsync(routingAgent);
}
else
{
    await RunScriptedAsync(routingAgent);
}

static async Task RunScriptedAsync(AIAgent routingAgent)
{
    const string request = "My laptop connects to Wi-Fi, but the VPN drops every few minutes and video calls freeze.";

    Console.WriteLine("===== Scripted IT routing =====");
    Console.WriteLine($"User: {request}");
    Console.WriteLine();

    await RunOneTurnAsync(routingAgent, request);
}

static async Task RunInteractiveAsync(AIAgent routingAgent)
{
    Console.WriteLine("Interactive IT routing. Type a service-desk request; empty line or exit stops.");

    while (true)
    {
        Console.Write("You: ");
        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input) ||
            string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        await RunOneTurnAsync(routingAgent, input);
    }
}

static async Task RunOneTurnAsync(AIAgent routingAgent, string userMessage)
{
    AgentResponse response = await routingAgent.RunAsync(userMessage);
    string handledBy = GetRespondingAgentName(response);
    string answer = GetRespondingAgentText(response);

    Console.WriteLine($"Handled by: {handledBy}");
    Console.WriteLine(answer);
    Console.WriteLine();
}

static string GetRespondingAgentName(AgentResponse response)
{
    return response.Messages
        .Select(message => message.AuthorName)
        .LastOrDefault(authorName => !string.IsNullOrWhiteSpace(authorName) &&
            !string.Equals(authorName, "ServiceDeskRouter", StringComparison.OrdinalIgnoreCase))
        ?? "Unknown";
}

static string GetRespondingAgentText(AgentResponse response)
{
    return response.Messages
        .LastOrDefault(message => !string.IsNullOrWhiteSpace(message.AuthorName) &&
            !string.Equals(message.AuthorName, "ServiceDeskRouter", StringComparison.OrdinalIgnoreCase))
        ?.Text
        ?? response.Text;
}
