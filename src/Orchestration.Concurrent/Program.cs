using Azure.AI.Projects;
using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Text.Json;

EnvLoader.LoadRepoRootEnv();

IChatClient chatClient = AgentClientFactory.CreateChatClient();

// Create translation agents for sequential processing
var translationAgents = (from lang in (string[])["French", "Spanish", "English"]
                         select GetTranslationAgent(lang, chatClient));
var workflow = AgentWorkflowBuilder.BuildConcurrent(translationAgents);

// Run the workflow
var messages = new List<ChatMessage> { new(ChatRole.User, "Hello, world!") };
List<ChatMessage> result = await RunWorkflowAsync(workflow, messages);

// Display aggregated results from all agents
Console.WriteLine("===== Final Aggregated Results =====");
foreach (var message in result)
{
    Console.WriteLine($"{message.Role}: {message.Text}");
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

static AIAgent GetTranslationAgent(string targetLanguage, IChatClient client) =>
    client.AsAIAgent(
        instructions: $"You are a translation assistant who only responds in {targetLanguage}. Respond to any " +
        $"input by outputting the name of the input language and then translating the input to {targetLanguage}.",
        name: $"TranslationAgent_{targetLanguage}");
