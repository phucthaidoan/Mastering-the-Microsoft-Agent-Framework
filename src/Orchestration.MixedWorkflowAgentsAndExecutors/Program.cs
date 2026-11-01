using System.Text.Json;
using System.Text.RegularExpressions;
using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

#pragma warning disable MAAIW001

namespace Orchestration.MixedWorkflowAgentsAndExecutors;

/// <summary>
/// This sample demonstrates how to seamlessly mix deterministic custom executors and AI agents
/// within a single workflow using Microsoft Agent Framework.
///
/// Scenario: Enterprise Customer Support & PII Sanitizer Pipeline
/// 1. TicketIntakeExecutor: Accepts raw customer request, assigns a Ticket ID, and initializes shared audit state.
/// 2. PiiSanitizerExecutor: Deterministically detects and masks PII (emails, phone numbers, credit cards) via regex.
/// 3. StringToChatMessageExecutor: Adapter executor converting sanitized string into ChatMessage + TurnToken.
/// 4. TriageAgent: AI agent classifying ticket category (Billing, Technical, Account, General) and urgency.
/// 5. TriageSyncExecutor: Adapter/Sync executor parsing triage results, updating state, and preparing context for resolution.
/// 6. ResolutionAgent: AI agent generating empathetic, policy-compliant resolution addressing the customer issue.
/// 7. AuditAndFinalOutputExecutor: Formats final response and renders an enterprise audit summary card.
/// </summary>
public static class Program
{
    private static async Task Main()
    {
        Console.WriteLine("\n=== Mixed Workflow: Agents and Custom Executors ===");
        Console.WriteLine("Demonstrating the Adapter Pattern & Chat Protocol in Microsoft Agent Framework\n");

        EnvLoader.LoadRepoRootEnv();
        IChatClient chatClient = AgentClientFactory.CreateChatClient();

        // 1. Instantiate deterministic and adapter executors
        TicketIntakeExecutor intake = new();
        PiiSanitizerExecutor sanitizer = new();
        StringToChatMessageExecutor stringToChatAdapter = new();
        TriageSyncExecutor triageSync = new();
        AuditAndFinalOutputExecutor finalOutput = new();

        // 2. Instantiate intelligent AI agents
        AIAgent triageAgent = chatClient.AsAIAgent(
            name: "TriageAgent",
            instructions: """
                You are an enterprise customer support triage specialist.
                Analyze the incoming sanitized support ticket and classify it accurately.

                Provide your response in EXACTLY this format:
                CATEGORY: [BILLING | TECHNICAL | ACCOUNT | GENERAL]
                PRIORITY: [LOW | MEDIUM | HIGH | CRITICAL]
                SUMMARY: <One concise sentence summarizing the core issue>
                POLICY_NOTE: <Any mandatory compliance or SLA note>

                Example:
                CATEGORY: BILLING
                PRIORITY: HIGH
                SUMMARY: Customer reports duplicate subscription charges requiring prompt reversal.
                POLICY_NOTE: Verify transaction history and escalate to Tier 2 finance if needed.
                """);

        AIAgent resolutionAgent = chatClient.AsAIAgent(
            name: "ResolutionAgent",
            instructions: """
                You are a senior enterprise customer support resolution engineer.
                Based on the provided ticket information and triage assessment, craft a clear, empathetic,
                and professional response to the customer.

                Guidelines:
                - Acknowledge their issue and state what concrete steps are being taken.
                - Never attempt to guess or hallucinate masked PII (e.g. [REDACTED_EMAIL], [REDACTED_ACCOUNT_NO]).
                - Maintain an authentic, supportive, enterprise-grade tone.
                """);

        // 3. Configure AI agent host options
        // By default, AIAgentHostOptions.ForwardIncomingMessages is true, which causes the agent
        // to forward incoming inputs as well as its generated output to downstream executors.
        // We set ForwardIncomingMessages = false so only newly generated agent responses flow forward.
        AIAgentHostOptions agentOptions = new() { ForwardIncomingMessages = false };
        ExecutorBinding triageAgentExecutor = triageAgent.BindAsExecutor(agentOptions);
        ExecutorBinding resolutionAgentExecutor = resolutionAgent.BindAsExecutor(agentOptions);

        // 4. Build the mixed workflow connecting executors and agents
        // Key concept: The Adapter Pattern bridges standard data types (string) to the Chat Protocol
        // (ChatMessage + TurnToken) expected by AIAgents.
        WorkflowBuilder workflowBuilder = new WorkflowBuilder(intake)
            .AddEdge(intake, sanitizer)
            .AddEdge(sanitizer, stringToChatAdapter)          // Adapter: string -> ChatMessage + TurnToken
            .AddEdge(stringToChatAdapter, triageAgentExecutor)// AI Agent for triage (without forwarding input)
            .AddEdge(triageAgentExecutor, triageSync)         // Adapter/Sync: List<ChatMessage> -> ChatMessage + TurnToken
            .AddEdge(triageSync, resolutionAgentExecutor)     // AI Agent for resolution (without forwarding input)
            .AddEdge(resolutionAgentExecutor, finalOutput)    // Deterministic final output & audit
            .WithOutputFrom(finalOutput);

        Workflow workflow = workflowBuilder.Build();

        // Display Mermaid workflow topology
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("=== Workflow Visualization (Mermaid) ===");
        Console.WriteLine(workflow.ToMermaidString());
        Console.WriteLine("========================================\n");
        Console.ResetColor();

        // Interactive execution loop
        Console.WriteLine("Enter a support ticket message (or press Enter for default test cases):");
        Console.Write("> ");
        string? userInput = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(userInput))
        {
            string[] testCases = [
                "Hello, I was charged twice for subscription ACCT-982341. My email is sarah.connor@cyberdyne.org and card is 4111-2222-3333-4444. Please refund immediately!",
                //"URGENT: Our production Kubernetes cluster in east-us region is throwing 500 errors. Call the on-call engineer at +1-555-839-2019 or ops-lead@techcorp.io right away.",
                //"Where can I find documentation on SSO integration and SAML 2.0 configuration for our enterprise workspace?"
            ];

            for (int i = 0; i < testCases.Length; i++)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"\n{new string('=', 80)}");
                Console.WriteLine($"[Test Case {i + 1}/{testCases.Length}]: \"{testCases[i]}\"");
                Console.WriteLine($"{new string('=', 80)}\n");
                Console.ResetColor();

                Workflow freshWorkflow = workflowBuilder.Build();
                await ExecuteWorkflowAsync(freshWorkflow, testCases[i]);

                if (i < testCases.Length - 1)
                {
                    Console.WriteLine("\nPress Enter to proceed to the next test case...");
                    Console.ReadLine();
                }
            }
        }
        else
        {
            await ExecuteWorkflowAsync(workflow, userInput);
        }

        // Continued REPL loop
        while (true)
        {
            Console.WriteLine("\nEnter another support ticket (or type 'exit' to quit):");
            Console.Write("> ");
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || string.Equals(input.Trim(), "exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            Workflow freshWorkflow = workflowBuilder.Build();
            await ExecuteWorkflowAsync(freshWorkflow, input.Trim());
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n✅ Sample Complete: Successfully demonstrated mixed agents and executors with adapter pattern.\n");
        Console.ResetColor();
    }

    private static async Task ExecuteWorkflowAsync(Workflow workflow, string input)
    {
        string? activeAgentExecutorId = null;

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input);

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            switch (evt)
            {
                case AgentResponseUpdateEvent agentUpdate:
                    if (!string.IsNullOrEmpty(agentUpdate.Update.Text))
                    {
                        if (agentUpdate.ExecutorId != activeAgentExecutorId)
                        {
                            activeAgentExecutorId = agentUpdate.ExecutorId;
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine($"\n--- [{activeAgentExecutorId} Streaming Output] ---");
                            Console.ResetColor();
                        }

                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write(agentUpdate.Update.Text);
                        Console.ResetColor();
                    }
                    break;

                case WorkflowOutputEvent:
                    // Final output rendered by AuditAndFinalOutputExecutor
                    break;

                case WorkflowErrorEvent workflowError:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"\nWorkflow Error: {workflowError.Exception?.Message ?? "Unknown workflow error."}");
                    Console.ResetColor();
                    break;

                case ExecutorFailedEvent executorFailed:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"\nExecutor '{executorFailed.ExecutorId}' failed: {executorFailed.Data}");
                    Console.ResetColor();
                    break;
            }
        }
    }
}

// ============================================================================
// Workflow Shared State Models
// ============================================================================

public static class WorkflowStateConstants
{
    public const string ScopeName = "SupportTicketScope";
    public const string AuditKey = "TicketAudit";
}

public sealed class TicketAuditRecord
{
    public string TicketId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int OriginalLength { get; set; }
    public int RedactedItemsCount { get; set; }
    public List<string> RedactedCategories { get; set; } = [];
    public string TriageCategory { get; set; } = "UNKNOWN";
    public string Priority { get; set; } = "MEDIUM";
    public string TriageSummary { get; set; } = string.Empty;
}

// ============================================================================
// Custom Executors
// ============================================================================

/// <summary>
/// Deterministic executor: receives raw customer input, assigns a ticket ID,
/// and registers initial telemetry in shared workflow state.
/// </summary>
internal sealed class TicketIntakeExecutor() : Executor<string, string>("TicketIntake")
{
    public override async ValueTask<string> HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        string ticketId = $"TKT-{Random.Shared.Next(10000, 99999)}";

        TicketAuditRecord audit = new()
        {
            TicketId = ticketId,
            CreatedAtUtc = DateTime.UtcNow,
            OriginalLength = message.Length
        };

        // Save audit state to the explicitly named shared scope
        await context.QueueStateUpdateAsync(WorkflowStateConstants.AuditKey, audit, WorkflowStateConstants.ScopeName, cancellationToken);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[{this.Id}] Received ticket payload ({message.Length} chars). Assigned ID: {ticketId}");
        Console.ResetColor();

        return message;
    }
}

/// <summary>
/// Deterministic executor: scans and masks sensitive Personally Identifiable Information (PII)
/// using compiled regular expressions, recording redaction audit logs into workflow state.
/// </summary>
internal sealed class PiiSanitizerExecutor() : Executor<string, string>("PiiSanitizer")
{
    private static readonly Regex EmailRegex = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"(?:\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex AccountCardRegex = new(@"(?:\b\d{4}[-\s]?){3}\d{4}\b|\bACCT-\d{4,}\b", RegexOptions.Compiled);

    public override async ValueTask<string> HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        int redactedCount = 0;
        List<string> categories = [];

        string sanitized = EmailRegex.Replace(message, _ =>
        {
            redactedCount++;
            if (!categories.Contains("Email")) categories.Add("Email");
            return "[REDACTED_EMAIL]";
        });

        sanitized = PhoneRegex.Replace(sanitized, _ =>
        {
            redactedCount++;
            if (!categories.Contains("Phone")) categories.Add("Phone");
            return "[REDACTED_PHONE]";
        });

        sanitized = AccountCardRegex.Replace(sanitized, _ =>
        {
            redactedCount++;
            if (!categories.Contains("Account/Financial")) categories.Add("Account/Financial");
            return "[REDACTED_FINANCIAL_INFO]";
        });

        // Retrieve existing audit state from shared scope
        TicketAuditRecord? audit = await context.ReadStateAsync<TicketAuditRecord>(WorkflowStateConstants.AuditKey, WorkflowStateConstants.ScopeName, cancellationToken);
        if (audit is not null)
        {
            audit.RedactedItemsCount = redactedCount;
            audit.RedactedCategories = categories;
            await context.QueueStateUpdateAsync(WorkflowStateConstants.AuditKey, audit, WorkflowStateConstants.ScopeName, cancellationToken);
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[{this.Id}] PII sanitization complete: {redactedCount} items redacted ({string.Join(", ", categories.DefaultIfEmpty("None"))}).");
        Console.WriteLine($"[{this.Id}] Sanitized payload: \"{sanitized}\"");
        Console.ResetColor();

        return sanitized;
    }
}

/// <summary>
/// Adapter Executor: Bridges deterministic executor output (string) to AI Agent Chat Protocol.
/// The Chat Protocol requires:
/// 1. A ChatMessage instance with User role.
/// 2. A TurnToken to trigger the agent's turn.
/// </summary>
[SendsMessage(typeof(ChatMessage))]
[SendsMessage(typeof(TurnToken))]
internal sealed class StringToChatMessageExecutor() : Executor<string>("StringToChatMessage")
{
    public override async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"[{this.Id}] Adapter triggered: Converting string into ChatMessage and dispatching TurnToken.");
        Console.ResetColor();

        ChatMessage chatMessage = new(ChatRole.User, message);

        // Send converted chat message and TurnToken to downstream agent
        await context.SendMessageAsync(chatMessage, cancellationToken: cancellationToken);
        await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken: cancellationToken);
    }
}

/// <summary>
/// Adapter / Sync Executor: Bridges AIAgent output (List&lt;ChatMessage&gt;) to downstream AIAgent.
/// 1. Extracts triage analysis.
/// 2. Updates shared workflow state.
/// 3. Formulates an enriched contextual prompt for the resolution agent.
/// 4. Dispatches ChatMessage + TurnToken to trigger the resolution agent.
/// </summary>
[SendsMessage(typeof(ChatMessage))]
[SendsMessage(typeof(TurnToken))]
internal sealed class TriageSyncExecutor() : Executor<List<ChatMessage>>("TriageSync")
{
    public override async ValueTask HandleAsync(List<ChatMessage> message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // Defensive check: only process responses generated by the agent (skip forwarded user messages)
        if (message.Count == 0 || message.All(m => m.Role == ChatRole.User))
        {
            return;
        }

        Console.WriteLine(); // Newline after streaming agent tokens
        string agentResponse = string.Join("\n", message.Select(m => m.Text ?? "")).Trim();

        string category = "GENERAL";
        string priority = "MEDIUM";
        string summary = agentResponse;

        foreach (string line in agentResponse.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("CATEGORY:", StringComparison.OrdinalIgnoreCase))
            {
                category = line.Substring("CATEGORY:".Length).Trim();
            }
            else if (line.StartsWith("PRIORITY:", StringComparison.OrdinalIgnoreCase))
            {
                priority = line.Substring("PRIORITY:".Length).Trim();
            }
            else if (line.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase))
            {
                summary = line.Substring("SUMMARY:".Length).Trim();
            }
        }

        // Read and enrich audit record in shared scope
        TicketAuditRecord? audit = await context.ReadStateAsync<TicketAuditRecord>(WorkflowStateConstants.AuditKey, WorkflowStateConstants.ScopeName, cancellationToken);
        if (audit is not null)
        {
            audit.TriageCategory = category;
            audit.Priority = priority;
            audit.TriageSummary = summary;
            await context.QueueStateUpdateAsync(WorkflowStateConstants.AuditKey, audit, WorkflowStateConstants.ScopeName, cancellationToken);
        }

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"[{this.Id}] Triage classified: Category=[{category}], Priority=[{priority}]");
        Console.WriteLine($"[{this.Id}] Preparing enriched handoff prompt for ResolutionAgent...");
        Console.ResetColor();

        string enrichedPrompt = $"""
            [SUPPORT TICKET CONTEXT]
            Ticket ID: {audit?.TicketId ?? "UNKNOWN"}
            Triage Category: {category}
            Urgency / Priority: {priority}
            Sanitization: {audit?.RedactedItemsCount ?? 0} sensitive items masked.

            Triage Analysis:
            {agentResponse}

            Please compose a comprehensive, empathetic customer resolution based on this triage.
            """;

        ChatMessage handoffMessage = new(ChatRole.User, enrichedPrompt);
        await context.SendMessageAsync(handoffMessage, cancellationToken: cancellationToken);
        await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken: cancellationToken);
    }
}

/// <summary>
/// Deterministic final output executor: collects the final resolution, reads all workflow
/// audit data from state, renders a structured enterprise audit card, and yields the final string.
/// </summary>
internal sealed class AuditAndFinalOutputExecutor() : Executor<List<ChatMessage>, string>("AuditAndFinalOutput")
{
    public override async ValueTask<string> HandleAsync(List<ChatMessage> message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // Defensive check: ignore empty or user prompts that may have been forwarded
        if (message.Count == 0 || message.All(m => m.Role == ChatRole.User))
        {
            return string.Empty;
        }

        string finalResponse = string.Join("\n", message.Select(m => m.Text ?? "")).Trim();
        TicketAuditRecord? audit = await context.ReadStateAsync<TicketAuditRecord>(WorkflowStateConstants.AuditKey, WorkflowStateConstants.ScopeName, cancellationToken);

        Console.WriteLine(); // Newline after streaming agent tokens
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n" + new string('─', 70));
        Console.WriteLine("                ENTERPRISE SUPPORT AUDIT CARD");
        Console.WriteLine(new string('─', 70));
        Console.WriteLine($" Ticket ID        : {audit?.TicketId ?? "N/A"}");
        Console.WriteLine($" Created (UTC)    : {audit?.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($" Category         : {audit?.TriageCategory ?? "N/A"}");
        Console.WriteLine($" Priority         : {audit?.Priority ?? "N/A"}");
        Console.WriteLine($" PII Redacted     : {audit?.RedactedItemsCount ?? 0} item(s) [{string.Join(", ", audit?.RedactedCategories ?? [])}]");
        Console.WriteLine($" Triage Summary   : {audit?.TriageSummary ?? "N/A"}");
        Console.WriteLine(new string('─', 70));
        Console.WriteLine(" FINAL CUSTOMER RESOLUTION:");
        Console.WriteLine(finalResponse);
        Console.WriteLine(new string('─', 70) + "\n");
        Console.ResetColor();

        return finalResponse;
    }
}
