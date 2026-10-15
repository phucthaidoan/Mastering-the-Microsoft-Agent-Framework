# Chapter 16: Route Advisor Server

This project exposes a small RouteAdvisor agent over the A2A protocol on a
fixed local URL.

Start this server before running the RouteAdvisor client.

Run from the repository root:

    dotnet run --project ./src/Ch16.RouteAdvisorServer/Ch16.RouteAdvisorServer.csproj

Local URLs:
- Server: http://localhost:5240
- Agent card: http://localhost:5240/.well-known/agent-card.json
- A2A endpoint: http://localhost:5240/a2a/route-advisor
