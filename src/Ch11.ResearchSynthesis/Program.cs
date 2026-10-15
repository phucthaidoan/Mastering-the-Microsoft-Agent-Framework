using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

EnvLoader.LoadRepoRootEnv();

IChatClient chatClient = AgentClientFactory.CreateChatClient();

const string researchQuestion = """
A regional service provider is considering a 90-day pilot for evening pickup lockers
at three transit stations. The goal is to reduce missed deliveries for customers who
cannot receive packages during business hours. The pilot would use staffed restocking,
temporary signage, and a limited catalog of eligible package sizes.

Analyze whether the pilot should move forward.
""";

AIAgent riskAnalyst = chatClient.AsAIAgent(
    instructions: """
    You are RiskAnalyst.
    Review the same business scenario from a risk angle only.
    Identify operational, customer, compliance, or cost risks.
    Keep the answer concise and grounded in the scenario.
    """,
    name: "RiskAnalyst");

AIAgent opportunityAnalyst = chatClient.AsAIAgent(
    instructions: """
    You are OpportunityAnalyst.
    Review the same business scenario from an opportunity angle only.
    Identify potential customer, operational, learning, or growth benefits.
    Keep the answer concise and grounded in the scenario.
    """,
    name: "OpportunityAnalyst");

Workflow workflow = AgentWorkflowBuilder.BuildConcurrent(
    "ResearchSynthesis",
    [riskAnalyst, opportunityAnalyst],
    CombineSpecialistMessages);

Console.WriteLine("===== Research question =====");
Console.WriteLine(researchQuestion);
Console.WriteLine();

List<ChatMessage> inputMessages = [new(ChatRole.User, researchQuestion)];
Run run = await InProcessExecution.RunAsync(workflow, inputMessages);

foreach (WorkflowEvent evt in run.NewEvents)
{
    if (evt is WorkflowOutputEvent outputEvent && outputEvent.Is<List<ChatMessage>>(out List<ChatMessage>? messages))
    {
        PrintSpecialistAndFinal(messages);
    }
}

static List<ChatMessage> CombineSpecialistMessages(IList<List<ChatMessage>> responses)
{
    string risk = GetResponseText(responses, "RiskAnalyst");
    string opportunity = GetResponseText(responses, "OpportunityAnalyst");

    PrintStage("RiskAnalyst output", risk);
    PrintStage("OpportunityAnalyst output", opportunity);

    string synthesized = $"""
    The pilot should move forward as a tightly scoped trial, not a full rollout.

    Risk view:
    {risk}

    Opportunity view:
    {opportunity}

    Synthesis:
    Proceed with the 90-day pilot if the team defines success measures, limits eligible package sizes, publishes clear customer instructions, and reviews staffing and station constraints weekly.
    """;

    return [new ChatMessage(ChatRole.Assistant, synthesized)];
}

static string GetResponseText(IList<List<ChatMessage>> responses, string agentName)
{
    List<ChatMessage>? agentMessages = responses.FirstOrDefault(messages =>
        messages.Any(message => string.Equals(message.AuthorName, agentName, StringComparison.OrdinalIgnoreCase)));

    return agentMessages?.LastOrDefault(message => message.Role == ChatRole.Assistant)?.Text.Trim()
        ?? "No response was produced.";
}

static void PrintSpecialistAndFinal(List<ChatMessage> messages)
{
    string final = messages.LastOrDefault()?.Text.Trim() ?? "No synthesized output was produced.";

    Console.WriteLine("===== Final synthesized output =====");
    Console.WriteLine(final);
}

static void PrintStage(string label, string text)
{
    Console.WriteLine($"===== {label} =====");
    Console.WriteLine(text);
    Console.WriteLine();
}
