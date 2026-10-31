# Handoffs Sample

This sample demonstrates dynamic handoff orchestration between specialized AI agents using `AgentWorkflowBuilder`.

A `triage_agent` receives user homework inquiries and routes them to the appropriate specialist (`math_tutor` or `history_tutor`). Specialist agents can hand off back to the triage agent for subsequent questions.

## Prerequisites

Configure your Microsoft Foundry / Azure AI Project credentials in your repository-root `.env` file or environment variables:

- `FOUNDRY_PROJECT_ENDPOINT` (or `AZURE_AI_PROJECT_ENDPOINT`)
- `FOUNDRY_MODEL` (or `AZURE_AI_MODEL_DEPLOYMENT_NAME`, defaults to `gpt-5.4-mini`)

Ensure you have authenticated with Azure (e.g., via `az login`).

## Running the Sample

Run the interactive console application:

```bash
dotnet run --project ./src/Orchestration.Handoffs/Orchestration.Handoffs.csproj
```

Type questions such as `"What was the French Revolution?"` or `"Solve 2x + 5 = 15"`. Type `exit` or press Enter on an empty line to exit.
