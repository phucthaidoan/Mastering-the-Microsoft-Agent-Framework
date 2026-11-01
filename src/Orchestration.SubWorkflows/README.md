# Sub-Workflows Sample

This sample demonstrates how to compose workflows hierarchically by embedding a workflow as an executor inside another workflow (sub-workflows) using Microsoft Agent Framework.

## Architecture & Scenario

Hierarchical sub-workflows allow you to encapsulate and reuse complex agent or step logic as modular units within higher-level workflows.

In this sample, we build a **Content & Research Pipeline**:

```
[User Topic] 
     │
     ▼
[Intake Agent] ──▶ [Research & Editorial Sub-Workflow] ──▶ [Publisher Agent] ──▶ [Final Briefing]
                         │
                         ├──▶ [Researcher Agent]
                         │          │
                         │          ▼
                         └──▶   [Editor Agent]
```

1. **Intake Agent** (`intake_agent`): Scopes research questions and defines an investigation outline from user input.
2. **Sub-Workflow** (`ResearchAndEditorialSubWorkflow`):
   - **Researcher Agent** (`researcher_agent`): Conducts research, gathering facts, data points, and market context.
   - **Editor Agent** (`editor_agent`): Refines, fact-checks, organizes, and structures the findings into a polished draft.
   - Composed sequentially and exposed via `.BindAsExecutor("ResearchAndEditorialSubWorkflow")`.
3. **Publisher Agent** (`publisher_agent`): Formats the edited draft into a publication-ready Executive Briefing.

## Prerequisites

Configure your Azure OpenAI credentials in your repository-root `.env` file or environment variables:

- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_DEPLOYMENT`

Ensure you have authenticated with Azure (e.g., via `az login`).

## Running the Sample

Run the interactive console application:

```bash
dotnet run --project ./src/Orchestration.SubWorkflows/Orchestration.SubWorkflows.csproj
```

Enter any research topic (e.g., `"Autonomous AI Agents in Enterprise Software"`). Type `exit` or press Enter on an empty line to exit.
