# Chapter 12: Market Assessment Group Chat

This project shows a round-robin group chat among three specialist agents: market,
operations, and finance. Each agent contributes one focused assessment of the same
product-launch scenario.

Book listing map:
- Program.cs: the three specialist agents.
- Program.cs: `CreateGroupChatBuilderWith(...)` with `RoundRobinGroupChatManager`.
- Program.cs: hosting the workflow as an `AIAgent` and running the scripted prompt.

Run from the repository root:

    dotnet run --project ./src/Ch12.MarketAssessmentGroupChat/Ch12.MarketAssessmentGroupChat.csproj
