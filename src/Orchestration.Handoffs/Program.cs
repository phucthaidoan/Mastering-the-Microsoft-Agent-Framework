using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

#pragma warning disable MAAIW001

EnvLoader.LoadRepoRootEnv();

var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT or AZURE_AI_PROJECT_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("FOUNDRY_MODEL")
    ?? Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? "gpt-5.4";

AIProjectClient aiProjectClient = new(new Uri(endpoint), new AzureCliCredential());

ChatClientAgent historyTutor = aiProjectClient.AsAIAgent(
    model: deploymentName,
    instructions: "You provide assistance with historical queries. Explain important events and context clearly. Only respond about history.",
    name: "history_tutor",
    description: "Specialist agent for historical questions");

ChatClientAgent mathTutor = aiProjectClient.AsAIAgent(
    model: deploymentName,
    instructions: "You provide help with math problems. Explain your reasoning at each step and include examples. Only respond about math.",
    name: "math_tutor",
    description: "Specialist agent for math questions");

ChatClientAgent triageAgent = aiProjectClient.AsAIAgent(
    model: deploymentName,
    instructions: "You determine which agent to use based on the user's homework question. ALWAYS handoff to another agent.",
    name: "triage_agent",
    description: "Routes messages to the appropriate specialist agent");

var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [mathTutor, historyTutor])
    .WithHandoffs([mathTutor, historyTutor], triageAgent)
    .Build();

Console.WriteLine("Homework Tutor Handoff Workflow initialized.");
Console.WriteLine("Type your question (e.g., 'What was the French Revolution?' or 'Solve 2x + 5 = 15'). Type 'exit' or press Enter on an empty line to quit.\n");

List<ChatMessage> messages = [];
while (true)
{
    Console.Write("Q: ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || string.Equals(input.Trim(), "exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    messages.Add(new(ChatRole.User, input));
    messages.AddRange(await RunWorkflowAsync(workflow, messages));
    Console.WriteLine();
}

static async Task<List<ChatMessage>> RunWorkflowAsync(Workflow workflow, List<ChatMessage> messages)
{
    string? lastExecutorId = null;

    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        if (evt is AgentResponseUpdateEvent e)
        {
            if (e.ExecutorId != lastExecutorId)
            {
                lastExecutorId = e.ExecutorId;
                Console.WriteLine();
                Console.WriteLine($"Executor: {e.ExecutorId}");
            }

            Console.Write(e.Update.Text);
            if (e.Update.Contents.OfType<FunctionCallContent>().FirstOrDefault() is FunctionCallContent call)
            {
                Console.WriteLine();
                Console.WriteLine($"  [Calling function '{call.Name}' with arguments: {JsonSerializer.Serialize(call.Arguments)}]");
            }
        }
        else if (evt is WorkflowOutputEvent output)
        {
            Console.WriteLine();
            return output.As<List<ChatMessage>>()!;
        }
        else if (evt is WorkflowErrorEvent workflowError)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(workflowError.Exception?.ToString() ?? "Unknown workflow error occurred.");
            Console.ResetColor();
        }
        else if (evt is ExecutorFailedEvent executorFailed)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Executor '{executorFailed.ExecutorId}' failed with {(executorFailed.Data == null ? "unknown error" : $"exception {executorFailed.Data}")}.");
            Console.ResetColor();
        }
    }

    return [];
}
