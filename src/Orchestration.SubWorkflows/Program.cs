using System.Text.Json;
using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

#pragma warning disable MAAIW001

/// <summary>
/// This sample demonstrates how to compose workflows hierarchically by embedding
/// a workflow as an executor inside another workflow (sub-workflows) using Microsoft Agent Framework.
///
/// Scenario: Content & Research Pipeline
/// 1. Intake Agent: Scopes research questions and defines an investigation outline from the user's prompt.
/// 2. Sub-Workflow (Research & Editorial):
///    - Researcher Agent: Gathers key findings, data points, and industry context.
///    - Editor Agent: Synthesizes, fact-checks, and refines the draft for clarity and consistency.
/// 3. Publisher Agent: Formats the final edited draft into an executive briefing with takeaways and future outlook.
/// </summary>

EnvLoader.LoadRepoRootEnv();

IChatClient chatClient = AgentClientFactory.CreateChatClient();

// Step 1: Create specialist agents
AIAgent intakeAgent = chatClient.AsAIAgent(
    name: "intake_agent",
    instructions: "You are a research intake specialist. Analyze the user's topic and outline 2-3 specific research questions " +
                  "and key angles for investigation. Produce a concise, structured research plan.");

AIAgent researcherAgent = chatClient.AsAIAgent(
    name: "researcher_agent",
    instructions: "You are an in-depth research analyst. Based on the research plan provided, gather concrete findings, " +
                  "facts, current trends, and insights addressing each question. Be informative, objective, and detailed.");

AIAgent editorAgent = chatClient.AsAIAgent(
    name: "editor_agent",
    instructions: "You are an editorial specialist. Review the researcher's findings, eliminate redundancies, organize into " +
                  "clear sections, ensure logical flow, and polish the tone into a coherent draft.");

AIAgent publisherAgent = chatClient.AsAIAgent(
    name: "publisher_agent",
    instructions: "You are an executive publisher. Transform the edited draft into a publication-ready Executive Briefing. " +
                  "Include: 1) Executive Summary, 2) Key Takeaways, 3) Core Findings, and 4) Outlook. Format nicely with markdown.");

// Step 2: Build the inner sub-workflow (Researcher -> Editor)
Console.WriteLine("Building sub-workflow: Researcher -> Editor...");
Workflow subWorkflow = AgentWorkflowBuilder.BuildSequential([researcherAgent, editorAgent]);

// Step 3: Bind the sub-workflow as an executor for use in the parent workflow
ExecutorBinding subWorkflowExecutor = subWorkflow.BindAsExecutor("ResearchAndEditorialSubWorkflow");

// Step 4: Build the parent workflow (Intake -> Sub-Workflow -> Publisher)
Console.WriteLine("Building parent workflow: Intake -> [Sub-Workflow] -> Publisher...\n");
Workflow mainWorkflow = new WorkflowBuilder(intakeAgent)
    .AddEdge(intakeAgent, subWorkflowExecutor)
    .AddEdge(subWorkflowExecutor, publisherAgent)
    .WithOutputFrom(publisherAgent)
    .Build();

// Display workflow structure in Mermaid format
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("=== Workflow Visualization (Mermaid) ===");
Console.WriteLine(mainWorkflow.ToMermaidString());
Console.WriteLine("========================================\n");
Console.ResetColor();

Console.WriteLine("=== Content & Research Sub-Workflow Pipeline ===");
Console.WriteLine("Enter a research topic (e.g., 'Autonomous AI Agents in Enterprise Software' or 'Quantum Computing Milestones').");
Console.WriteLine("Type 'exit' or press Enter on an empty line to quit.\n");

while (true)
{
    Console.Write("Topic: ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || string.Equals(input.Trim(), "exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    List<ChatMessage> messages = [new(ChatRole.User, input)];
    await RunWorkflowAsync(mainWorkflow, messages);
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
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n--- [{e.ExecutorId}] ---");
                Console.ResetColor();
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
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n=== Pipeline Completed Successfully ===");
            Console.ResetColor();
            return output.As<List<ChatMessage>>() ?? [];
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
