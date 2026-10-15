using A2A;
using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;

const string serverUrl = "http://localhost:5240";

EnvLoader.LoadRepoRootEnv();

A2ACardResolver resolver = new(new Uri(serverUrl));
AgentCard card = await resolver.GetAgentCardAsync();

Console.WriteLine("===== Discovered agent card =====");
Console.WriteLine($"Name: {card.Name}");
Console.WriteLine($"Description: {card.Description}");
Console.WriteLine($"Version: {card.Version}");
Console.WriteLine($"Endpoint: {card.SupportedInterfaces[0].Url}");
Console.WriteLine();

AIAgent agent = await resolver.GetAIAgentAsync();

const string question =
    "A 900 lb refrigerated shipment must move from an inland warehouse to a coastal dock by tomorrow afternoon. What routing approach should we use?";

Console.WriteLine("===== A2A RunAsync call =====");
Console.WriteLine($"Question: {question}");
AgentResponse response = await agent.RunAsync(question);
Console.WriteLine("Response:");
Console.WriteLine(response.Text);
Console.WriteLine();

const string streamingQuestion =
    "Stream a shorter advisory for a fragile pallet moving overnight across two regional hubs.";

Console.WriteLine("===== A2A RunStreamingAsync call =====");
Console.WriteLine($"Question: {streamingQuestion}");
Console.WriteLine("Streamed response:");

await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(streamingQuestion))
{
    Console.Write(update.Text);
}

Console.WriteLine();
