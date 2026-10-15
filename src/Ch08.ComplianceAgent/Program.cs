using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

EnvLoader.LoadRepoRootEnv();

IChatClient chatClient = AgentClientFactory.CreateChatClient();

string repoRoot = FindRepoRoot();
string serverProjectPath = Path.Combine(
    repoRoot,
    "src",
    "Ch08.ComplianceMcpServer",
    "Ch08.ComplianceMcpServer.csproj");

await using McpClient mcpClient = await McpClient.CreateAsync(
    new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "ComplianceMcpServer",
        Command = "dotnet",
        Arguments = ["run", "--project", serverProjectPath]
    }));

IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync();

Console.WriteLine("Discovered MCP tools");
foreach (McpClientTool tool in mcpTools)
{
    Console.WriteLine($"- {tool.Name}: {tool.Description}");
}

Console.WriteLine();

const string instructions = """
You are a compliance-monitoring assistant.
Answer only from the connected compliance tools.
Use the tools for compliance control status, failing controls, and control details.
If the tools cannot answer the question, say that the compliance tools do not contain that information.
Do not invent control IDs, statuses, dates, or remediation details.
""";

AIAgent agent = chatClient.AsAIAgent(
    instructions: instructions,
    name: "ComplianceAgent",
    tools: [.. mcpTools.Cast<AITool>()]);

string[] questions =
[
    "What is the status of control AC-2?",
    "Which controls are currently failing?",
    "Give me the details for backup-retention.",
    "What is our policy for employee vacation carryover?"
];

for (int index = 0; index < questions.Length; index++)
{
    string question = questions[index];
    Console.WriteLine($"Q{index + 1}: {question}");
    AgentResponse response = await agent.RunAsync(question);
    Console.WriteLine(response.Text);
    Console.WriteLine();
}

static string FindRepoRoot()
{
    DirectoryInfo? current = new(AppContext.BaseDirectory);

    while (current is not null)
    {
        bool hasSolution = File.Exists(Path.Combine(current.FullName, "MasteringAgentFramework.sln"));
        bool hasSrc = Directory.Exists(Path.Combine(current.FullName, "src"));

        if (hasSolution && hasSrc)
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Could not find the repository root.");
}
