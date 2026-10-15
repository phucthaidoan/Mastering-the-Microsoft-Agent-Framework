using System.ComponentModel;
using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

EnvLoader.LoadRepoRootEnv();

IChatClient chatClient = AgentClientFactory.CreateChatClient();

IReadOnlyDictionary<string, string> policies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["annual-leave"] = "Annual leave: Full-time employees receive 22 working days of annual leave each calendar year. Up to 4 unused days may be carried into the next year and must be used by April 30. Leave should be requested at least 10 working days before the first day away.",
    ["expense-reimbursement"] = "Expense reimbursement: Approved business expenses are reimbursed when submitted within 30 days with an itemized receipt. Client meals are reimbursable up to 80 dollars per person before tax and tip. Personal purchases, fines, and gifts over 50 dollars are not reimbursable.",
    ["remote-work"] = "Remote work: Employees may work remotely up to three days per week with manager approval. Remote-work days must be recorded on the team calendar. Work involving restricted records must be performed from an approved private network."
};

AIAgent policyExpert = chatClient.AsAIAgent(
    instructions: """
    You are a focused workplace policy expert.
    Use the LookUpPolicy tool before answering policy questions.
    Answer only from the policy text returned by the tool.
    If the requested topic is not covered by the policy text, say that it is not covered.
    Do not invent policy details.
    """,
    name: "PolicyExpert",
    description: "Answers questions about company workplace policies such as leave, expenses, and remote work.",
    tools: [AIFunctionFactory.Create(LookUpPolicy)]);

AIAgent mainAgent = chatClient.AsAIAgent(
    instructions: """
    You are a general assistant.
    For workplace-policy questions, use the PolicyExpert tool.
    For non-policy questions, answer normally without using the policy tool.
    Keep answers concise and clear.
    """,
    name: "Assistant",
    tools: [policyExpert.AsAIFunction()]);

string mode = args.Length > 0 ? args[0] : "scripted";

if (string.Equals(mode, "interactive", StringComparison.OrdinalIgnoreCase))
{
    await RunInteractiveAsync(mainAgent);
}
else
{
    await RunScriptedAsync(mainAgent);
}

[Description("Looks up invented workplace policy text by topic.")]
string LookUpPolicy(
    [Description("The policy topic or id, such as annual-leave, expense-reimbursement, or remote-work.")]
    string topic)
{
    string normalizedTopic = topic.Trim().ToLowerInvariant();

    if (policies.TryGetValue(normalizedTopic, out string? policy))
    {
        return policy;
    }

    foreach (KeyValuePair<string, string> entry in policies)
    {
        if (normalizedTopic.Contains(entry.Key, StringComparison.OrdinalIgnoreCase)
            || entry.Value.Contains(normalizedTopic, StringComparison.OrdinalIgnoreCase))
        {
            return entry.Value;
        }
    }

    return $"No workplace policy is available for topic '{topic}'. Covered topics are annual-leave, expense-reimbursement, and remote-work.";
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
    Console.WriteLine("Interactive mode. Policy questions route to the PolicyExpert skill. Press Enter or type exit to stop.");
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
