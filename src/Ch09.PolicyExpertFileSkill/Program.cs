using System.ComponentModel;
using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

EnvLoader.LoadRepoRootEnv();

IChatClient chatClient = AgentClientFactory.CreateChatClient();

#pragma warning disable MAAI001 // Agent Skills are experimental in the pinned 1.6.1 package.
AgentSkillsProvider skillsProvider = new(
    skillPath: Path.Combine(AppContext.BaseDirectory, "Skills"));
#pragma warning restore MAAI001

AIAgent mainAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "Assistant",
    ChatOptions = new ChatOptions
    {
        Instructions = """
        You are a general assistant.
        For workplace-policy questions, load and follow the policy-expert skill before answering.
        For non-policy questions, answer normally without loading the policy skill,
        and append a note that the question is a non-policy question.
        Keep answers concise and clear.
        """,
        Tools = [AIFunctionFactory.Create(LookUpPolicy)]
    },
    AIContextProviders = [skillsProvider]
});

string mode = args.Length > 0 ? args[0] : "scripted";

if (string.Equals(mode, "interactive", StringComparison.OrdinalIgnoreCase))
{
    await RunInteractiveAsync(mainAgent);
}
else
{
    await RunScriptedAsync(mainAgent);
}

[Description("Looks up invented workplace policy text by topic. Returns a not-covered message for unknown topics.")]
static string LookUpPolicy(
    [Description("The policy topic id: annual-leave, expense-reimbursement, or remote-work. For other topics, use a short topic id such as office-parking.")]
    string topic)
{
    return topic.Trim().ToLowerInvariant() switch
    {
        "annual-leave" => "Annual leave: Full-time employees receive 22 working days of annual leave each calendar year. Up to 4 unused days may be carried into the next year and must be used by April 30. Leave should be requested at least 10 working days before the first day away.",
        "expense-reimbursement" => "Expense reimbursement: Approved business expenses are reimbursed when submitted within 30 days with an itemized receipt. Client meals are reimbursable up to 80 dollars per person before tax and tip. Personal purchases, fines, and gifts over 50 dollars are not reimbursable.",
        "remote-work" => "Remote work: Employees may work remotely up to three days per week with manager approval. Remote-work days must be recorded on the team calendar. Work involving restricted records must be performed from an approved private network.",
        _ => $"No workplace policy is available for topic '{topic}'. Covered topics are annual-leave, expense-reimbursement, and remote-work."
    };
}

static async Task RunScriptedAsync(AIAgent agent)
{
    string[] questions =
    [
        "How many days of annual leave do I get?",
        "Can I expense a client dinner?",
        "What is 12 times 8?",
        "What is the policy for office parking?"
    ];

    for (int index = 0; index < questions.Length; index++)
    {
        string question = questions[index];
        Console.WriteLine($"Q{index + 1}: {question}");
        AgentResponse response = await agent.RunAsync(question);
        Console.WriteLine(response.Text);
        Console.WriteLine();
    }
}

static async Task RunInteractiveAsync(AIAgent agent)
{
    Console.WriteLine("Interactive mode. Policy questions use the file-based policy-expert skill. Press Enter or type exit to stop.");
    AgentSession session = await agent.CreateSessionAsync();

    while (true)
    {
        Console.Write("You: ");
        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input) ||
            string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        AgentResponse response = await agent.RunAsync(input, session);
        Console.WriteLine(response.Text);
        Console.WriteLine();
    }
}
