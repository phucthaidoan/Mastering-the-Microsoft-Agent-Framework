# Shared Library (`src/Shared`) Guidelines for AI Agents

## Overview

The `src/Shared` library is the core cross-cutting project in this repository. It provides common infrastructure for environment configuration, agent client initialization, and console output formatting.

Because `src/Shared` is referenced by virtually every chapter project in the solution (~20+ projects across Chapters 2–16 and Orchestration samples), changes made here have a wide blast radius. AI agents modifying or consuming this library must follow the guidelines detailed below.

---

## Component Breakdown & Usage Patterns

### 1. `EnvLoader` (`EnvLoader.cs`)

- **Role**: Automatically discovers and loads key-value pairs from the repository-root `.env` file into process environment variables.
- **Discovery Mechanism**: Starts from `AppContext.BaseDirectory` and ascends directory by directory until it finds `MasteringAgentFramework.sln`.
- **Fault Tolerance**: If `.env` is missing, it gracefully no-ops, allowing CI/CD pipelines and host environments to supply environment variables directly.
- **Consumer Pattern**:
  Every chapter project entry point (`Program.cs`) should call `EnvLoader.LoadRepoRootEnv()` as its very first statement before reading environment variables or initializing clients:

```csharp
using MasteringAgentFramework.Shared;

EnvLoader.LoadRepoRootEnv();
```

---

### 2. `AgentClientFactory` (`AgentClientFactory.cs`)

- **Role**: Central factory for creating pre-configured AI clients and agents.
- **Authentication Model**: Exclusively keyless using `AzureCliCredential` (for local development via `az login`) and Managed Identity. Do **not** use API keys or secrets.
- **Endpoint Normalization**: Azure AI Foundry portal endpoints often include suffixes like `/openai/v1/responses`, `/openai/v1`, `/responses`, or `/openai`. `AgentClientFactory` strips trailing paths automatically via `NormalizeAzureOpenAIEndpoint` to avoid 404 URL resolution errors.
- **Factory Methods**:
  - `AgentClientFactory.CreateChatClient()`: Returns an `IChatClient` backed by Azure OpenAI.
  - `AgentClientFactory.CreateFoundryProjectAgent(string instructions, string name)`: Returns an `AIAgent` backed by Azure AI Foundry `AIProjectClient`.

#### Required Environment Variables

| Variable Name | Required By | Description |
|---|---|---|
| `AZURE_OPENAI_ENDPOINT` | `CreateChatClient` | Base URL of the Azure OpenAI / Foundry resource |
| `AZURE_OPENAI_DEPLOYMENT` | `CreateChatClient` | Deployment / model name for chat completions |
| `AZURE_AI_PROJECT_ENDPOINT` | `CreateFoundryProjectAgent` | Azure AI Foundry Project connection string/endpoint |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | `CreateFoundryProjectAgent` | Deployed model name within the Foundry project |

#### Consumer Pattern:

```csharp
using MasteringAgentFramework.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

EnvLoader.LoadRepoRootEnv();

// Option A: Azure OpenAI IChatClient
IChatClient chatClient = AgentClientFactory.CreateChatClient();

// Option B: Foundry Project Agent
AIAgent agent = AgentClientFactory.CreateFoundryProjectAgent(
    instructions: "You are a customer support agent.",
    name: "TriageAgent");
```

---

### 3. `ConsoleRenderer` (`ConsoleRenderer.cs`)

- **Role**: Standardized helper for rendering streaming agent responses to the terminal.
- **Methods**:
  - `WriteStreamingResponseAsync(IAsyncEnumerable<AgentResponseUpdate> updates, CancellationToken cancellationToken = default)`: Streams response tokens chunk-by-chunk to `Console.Write` and emits a concluding newline.
  - `WriteUpdate(AgentResponseUpdate update)`: Renders an individual `AgentResponseUpdate` token.
- **Consumer Pattern**:

```csharp
using MasteringAgentFramework.Shared;

IAsyncEnumerable<AgentResponseUpdate> updates = agent.RunStreamingAsync("User prompt here");
await ConsoleRenderer.WriteStreamingResponseAsync(updates);
```

---

## Rules for Modifying `src/Shared` (Maintainer Guardrails)

When working on `src/Shared` itself, agents must adhere to the following rules:

1. **Preserve Backwards Compatibility (High Blast Radius)**:
   - Any signature change or method removal in `src/Shared` can break ~20 dependent projects.
   - Do not rename or remove existing public methods (`LoadRepoRootEnv`, `CreateChatClient`, `CreateFoundryProjectAgent`, `WriteStreamingResponseAsync`, `WriteUpdate`).
   - If extending APIs, use method overloads or optional parameters with sensible defaults.

2. **Rule of Three for New Additions**:
   - Only add new utilities, extensions, or classes to `src/Shared` if they are required by **at least three** distinct chapter projects.
   - Scenario-specific tools, prompts, models, or workflows belong inside their specific chapter projects (e.g., `Ch04.RetailReturns`, `Ch08.ComplianceAgent`), never in `Shared`.

3. **Strict Keyless Authentication & Environment Variables**:
   - Never add API key parameters or fallback credentials (e.g. `AzureKeyCredential`) to `AgentClientFactory`.
   - If a new environment variable is introduced:
     - Document it in this file (`src/Shared/AGENTS.md`).
     - Add the key with explanatory placeholder comments to `.env.template` at the repository root.

4. **Minimal Dependencies**:
   - Keep `Shared.csproj` dependencies restricted to official Microsoft agent and Azure abstractions:
     - `Microsoft.Agents.AI`
     - `Microsoft.Agents.AI.OpenAI`
     - `Microsoft.Agents.AI.Foundry`
     - `Azure.AI.OpenAI`
     - `Azure.AI.Projects`
     - `Azure.Identity`
     - `Microsoft.Extensions.Configuration*`
   - Do not add web host packages, third-party vector DB clients, or UI frameworks to `Shared`.

5. **Code Style & Structure**:
   - Target framework: `net8.0`.
   - Nullable reference types enabled (`<Nullable>enable</Nullable>`).
   - File-scoped namespace: `namespace MasteringAgentFramework.Shared;`.
   - Static utility classes should be declared `public static class`.
   - Async methods must accept an optional `CancellationToken` and end with the `Async` suffix.

---

## Verification & Build Workflow

Before committing changes to `src/Shared`, verify the entire repository:

1. **Full Solution Build**:
   Ensure all dependent chapter projects still compile cleanly:
   ```powershell
   ./tools/verify-repo.ps1
   ```
   Or using the .NET CLI:
   ```powershell
   dotnet build .\MasteringAgentFramework.sln
   ```

2. **Capture Command Output**:
   When diagnosing build issues across the solution, redirect output to inspect errors thoroughly:
   ```powershell
   dotnet build .\MasteringAgentFramework.sln --tl:off 2>&1 | Out-File $env:TEMP\build.log
   ```

3. **Smoke Test a Dependent Chapter**:
   Run a representative project to confirm runtime initialization succeeds:
   ```powershell
   dotnet run --project src/Ch02.CustomerSupportTriage
   ```
