using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

EnvLoader.LoadRepoRootEnv();

IChatClient chatClient = AgentClientFactory.CreateChatClient();

const string rawDocument = """
NOTICE: equipment room access update

Summary: the north equipment room will move to badge-only access.
Effective date: August 12.
Affected teams: field support, inventory, and facilities.
Action required: team leads must confirm badge lists by August 5.
Fallback: urgent access can be requested through the facilities desk.
""";

AIAgent summarizeAgent = chatClient.AsAIAgent(
    instructions: """
    You summarize cleaned document text.
    Use only the provided text.
    Produce a concise plain-language summary in two sentences or fewer.
    Do not add facts that are not present in the input.
    """,
    name: "Summarize");

ExecutorBinding extract = ((Func<string, string>)ExtractDocument).BindAsExecutor("Extract");
ExecutorBinding summarize = ((Func<string, ValueTask<string>>)SummarizeDocumentAsync).BindAsExecutor("Summarize");
ExecutorBinding format = ((Func<string, string>)FormatSummary).BindAsExecutor("Format");

WorkflowBuilder builder = new(extract);
builder.AddEdge(extract, summarize);
builder.AddEdge(summarize, format);
builder.WithOutputFrom(format);
Workflow workflow = builder.Build();

Console.WriteLine("===== Raw input =====");
Console.WriteLine(rawDocument);
Console.WriteLine();

string? finalDocument = null;

Run run = await InProcessExecution.RunAsync(workflow, rawDocument);
ProcessEvents(run.NewEvents, ref finalDocument);

Console.WriteLine("===== Final formatted document =====");
Console.WriteLine(finalDocument ?? "No final document was produced.");

static void ProcessEvents(IEnumerable<WorkflowEvent> events, ref string? finalDocument)
{
    foreach (WorkflowEvent evt in events)
    {
        if (evt is WorkflowOutputEvent outputEvent && outputEvent.Is<string>(out string? output))
        {
            finalDocument = output;
            continue;
        }

    }
}

static string ExtractDocument(string document)
{
    string[] lines = document
        .Split(Environment.NewLine, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim())
        .Where(line => line.Length > 0)
        .ToArray();

    string cleanedDocument = string.Join(Environment.NewLine, lines);
    PrintStage("Extract", cleanedDocument);
    return cleanedDocument;
}

async ValueTask<string> SummarizeDocumentAsync(string cleanedDocument)
{
    AgentResponse response = await summarizeAgent.RunAsync(cleanedDocument);
    PrintStage("Summarize", response.Text);
    return response.Text;
}

static string FormatSummary(string summary)
{
    string formatted = $"""
    DOCUMENT PIPELINE RESULT
    ------------------------
    {summary.Trim()}
    """;

    PrintStage("Format", formatted);
    return formatted;
}

static void PrintStage(string stageName, string value)
{
    Console.WriteLine($"===== {stageName} output =====");
    Console.WriteLine(value);
    Console.WriteLine();
}
