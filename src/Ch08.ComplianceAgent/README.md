# Chapter 8: Compliance MCP Agent

This console app launches the local compliance MCP server, discovers its tools,
and gives those tools to an agent built with the shared Azure OpenAI chat client.

Book listing map:
- `Program.cs`: starts the stdio MCP client, lists tools, builds the agent, and
  runs the scripted questions.
- `../Ch08.ComplianceMcpServer/ComplianceTools.cs`: the local MCP tool surface.

Run from the repository root:

    dotnet run --project ./src/Ch08.ComplianceAgent/Ch08.ComplianceAgent.csproj
