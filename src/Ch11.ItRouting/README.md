# Chapter 11B: IT Routing

This project demonstrates handoff orchestration. A triage agent routes IT
service-desk requests to either network support or access support.

Book listing map:
- `Program.cs`: triage and specialist agents, handoff workflow construction, the
  scripted run, and the interactive loop.

Scripted run:

    dotnet run --project ./src/Ch11.ItRouting/Ch11.ItRouting.csproj

Interactive run:

    dotnet run --project ./src/Ch11.ItRouting/Ch11.ItRouting.csproj -- interactive
