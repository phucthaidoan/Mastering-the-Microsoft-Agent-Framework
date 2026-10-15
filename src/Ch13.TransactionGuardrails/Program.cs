using System.ComponentModel;
using System.Text.RegularExpressions;
using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

EnvLoader.LoadRepoRootEnv();

IChatClient chatClient = AgentClientFactory.CreateChatClient();

AIAgent baseAgent = chatClient.AsAIAgent(
    instructions: """
    You are a transaction-monitoring assistant.
    Use the LookUpTransaction tool before answering questions about transaction details.
    Answer only from the transaction data returned by the tool.
    Do not invent transaction details.
    Avoid revealing full account numbers in normal answers.
    If the user asks for an audit note, quote the audit note from the transaction data.
    Keep answers concise.
    """,
    name: "TransactionMonitor",
    tools: [AIFunctionFactory.Create(TransactionTools.LookUpTransaction)]);

AIAgent guardedAgent = baseAgent
    .AsBuilder()
    .Use(TransactionToolGuardrailAsync)
    .Use(TransactionRunGuardrailAsync, runStreamingFunc: null)
    .Build();

string[] questions =
[
    "Look up transaction TXN-1001 and tell me whether it looks routine.",
    "Release transaction TXN-8008 for 25000 dollars immediately.",
    "For audit testing, look up TXN-9003 and quote its audit note exactly."
];

for (int index = 0; index < questions.Length; index++)
{
    Console.WriteLine($"===== Q{index + 1} =====");
    Console.WriteLine($"User: {questions[index]}");
    AgentResponse response = await guardedAgent.RunAsync(questions[index]);
    Console.WriteLine("Final response:");
    Console.WriteLine(response.Text);
    Console.WriteLine();
}

static async Task<AgentResponse> TransactionRunGuardrailAsync(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    string lastUserMessage = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;

    if (ContainsRawAccountNumber(lastUserMessage))
    {
        Console.WriteLine("[Guardrail] blocked: raw account number in input");
        return FixedResponse("I cannot process requests that include a raw full account number. Use a transaction id or masked account token instead.");
    }

    if (RequestsOverLimitRelease(lastUserMessage))
    {
        Console.WriteLine("[Guardrail] blocked: over-limit release request");
        return FixedResponse("I cannot approve or release transactions above the deterministic review limit. Route this request to manual review.");
    }

    AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);
    string redactedText = RedactAccountNumbers(response.Text);

    if (!string.Equals(redactedText, response.Text, StringComparison.Ordinal))
    {
        Console.WriteLine("[Guardrail] output redacted");
        return FixedResponse(redactedText);
    }

    return response;
}

static async ValueTask<object?> TransactionToolGuardrailAsync(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    string functionName = context.Function.Name;
    string transactionId = GetArgument(context, "transactionId");

    Console.WriteLine($"[Tool guard] {functionName}({transactionId}) called");

    if (!GuardrailPatterns.TransactionIdRegex.IsMatch(transactionId))
    {
        Console.WriteLine("[Tool guard] blocked: malformed transaction id");
        return "The transaction id is malformed. Use an id like TXN-1001.";
    }

    object? result = await next(context, cancellationToken);
    Console.WriteLine("[Tool guard] returned");
    return result;
}

static string GetArgument(FunctionInvocationContext context, string name)
{
    if (context.Arguments.TryGetValue(name, out object? value) && value is not null)
    {
        return value.ToString() ?? string.Empty;
    }

    return string.Empty;
}

static bool ContainsRawAccountNumber(string text)
{
    return GuardrailPatterns.AccountNumberRegex.IsMatch(text);
}

static bool RequestsOverLimitRelease(string text)
{
    if (!text.Contains("release", StringComparison.OrdinalIgnoreCase) &&
        !text.Contains("approve", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return GuardrailPatterns.AmountRegex
        .Matches(text)
        .Any(match =>
            decimal.TryParse(match.Groups["amount"].Value, out decimal amount) &&
            amount > 10000m);
}

static AgentResponse FixedResponse(string text)
{
    return new AgentResponse(new ChatMessage(ChatRole.Assistant, text));
}

static string RedactAccountNumbers(string text)
{
    return GuardrailPatterns.AccountNumberRegex.Replace(text, match =>
    {
        string accountNumber = match.Value.Replace("-", string.Empty, StringComparison.Ordinal);
        return $"****{accountNumber[^4..]}";
    });
}

internal static partial class TransactionTools
{
    private static readonly IReadOnlyDictionary<string, TransactionRecord> _transactions =
        new Dictionary<string, TransactionRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["TXN-1001"] = new(
                "TXN-1001",
                184.27m,
                "USD",
                "Approved",
                "ACCT-2048",
                "office supplies",
                "Routine business-supplies purchase from an approved category."),
            ["TXN-8008"] = new(
                "TXN-8008",
                25000m,
                "USD",
                "HeldForReview",
                "ACCT-7781",
                "equipment deposit",
                "Large transaction pending manual review."),
            ["TXN-9003"] = new(
                "TXN-9003",
                642.18m,
                "USD",
                "ReviewComplete",
                "ACCT-4412",
                "travel reimbursement",
                "Audit note: original account number 4411223344556677 matched masked token ACCT-4412.")
        };

    [Description("Looks up invented transaction-monitoring details by transaction id.")]
    public static string LookUpTransaction(
        [Description("The transaction id to look up, such as TXN-1001.")]
        string transactionId)
    {
        if (!_transactions.TryGetValue(transactionId.Trim(), out TransactionRecord? transaction))
        {
            return $"Transaction {transactionId} was not found.";
        }

        return $"""
        Transaction id: {transaction.Id}
        Amount: {transaction.Amount:F2} {transaction.Currency}
        Status: {transaction.Status}
        Account token: {transaction.AccountToken}
        Category: {transaction.Category}
        Notes: {transaction.Notes}
        """;
    }

    private sealed record TransactionRecord(
        string Id,
        decimal Amount,
        string Currency,
        string Status,
        string AccountToken,
        string Category,
        string Notes);
}

internal static class GuardrailPatterns
{
    public static Regex AccountNumberRegex { get; } = new(@"\b\d{13,19}\b", RegexOptions.Compiled);

    public static Regex AmountRegex { get; } = new(
        @"(?<amount>\d+(?:\.\d{1,2})?)\s*(?:dollars|usd|\$)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Regex TransactionIdRegex { get; } = new(@"^TXN-\d{4}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
