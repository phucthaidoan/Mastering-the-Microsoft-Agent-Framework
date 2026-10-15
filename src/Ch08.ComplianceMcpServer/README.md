# Chapter 8: Compliance MCP Server

This console app is a local MCP server over stdio. It exposes deterministic
compliance-monitoring tools from `ComplianceTools`, with no external account,
token, or internet dependency.

Book listing map:
- `Program.cs`: hosts the MCP stdio server.
- `ComplianceTools.cs`: exposes the compliance tools and in-memory control data.

Run the chapter through the client project; it launches this server automatically:

    dotnet run --project ./src/Ch08.ComplianceAgent/Ch08.ComplianceAgent.csproj
