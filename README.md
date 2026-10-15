# Mastering the Microsoft Agent Framework: Companion Code

This repository holds the complete, runnable code for the book. Every code listing
in the book corresponds to a real program here, built against one pinned version of
the Microsoft Agent Framework (1.6.1). If the book shows you a block of code, the
file it came from is in this repository and it builds.

## What you need

- The .NET 8 SDK. The exact version is pinned in `global.json`.
- An Azure AI Foundry project with a deployed chat model. Chapter 2 walks through
  deploying one. Chapter 6 additionally needs a deployed embedding model.
- The Azure CLI, signed in with `az login`. The code authenticates with your CLI
  identity in development and a managed identity in production. It does not use API
  keys; Chapter 3 explains why.

## First run

From the repository root, copy the environment template and fill in your values:

    cp .env.template .env

Then verify the whole solution builds:

    ./tools/verify-repo.ps1      # Windows
    ./tools/verify-repo.sh       # macOS and Linux

A clean run means your environment matches the book's. You can also just run
`dotnet build` from the root, since the whole book is a single solution.

## How the chapters map to the code

| Chapter | Project | What it builds |
| --- | --- | --- |
| 2 | src/Ch02.CustomerSupportTriage | First agent: a support triage assistant |
| 3 | src/Ch03.ProvidersAndIdentity | The same agent over the native Foundry path |
| 4 | src/Ch04.RetailReturns | An agent that calls tools |
| 5 | src/Ch05.AppointmentIntake | An agent with memory |
| 6 | src/Ch06.Rag | An agent grounded in your documents (RAG) |
| 7 | src/Ch07.ClaimsIntake | An agent that asks a human before acting |
| 8 | src/Ch08.ComplianceAgent, src/Ch08.ComplianceMcpServer | An agent that uses tools over MCP, plus an MCP server |
| 9 | src/Ch09.PolicyExpertSkill | An agent that draws on a packaged skill |
| 10 | src/Ch10.DocumentPipeline | A sequential multi-step workflow |
| 11 | src/Ch11.ResearchSynthesis, src/Ch11.ItRouting | Concurrent fan-out/fan-in and handoff routing |
| 12 | src/Ch12.MarketAssessmentGroupChat, src/Ch12.MarketAssessmentMagentic | Group chat and Magentic orchestration |
| 13 | src/Ch13.TransactionGuardrails | Middleware and input/output guardrails |
| 14 | src/Ch14.LogisticsObservability | An observable agent traced with OpenTelemetry |
| 15 | src/Ch15.LogisticsEvaluation | Evaluating an agent with deterministic and LLM-judge tests |
| 16 | src/Ch16.RouteAdvisorServer, src/Ch16.RouteAdvisorClient | One agent calling another over A2A |

Chapters 1 and 17 and the appendix are discussion chapters with no code project of their own.

Shared connection and helper code lives in `src/Shared`. Each chapter project has
its own README mapping files to the book's listings.

## A Note On Cost And Preview Packages

The samples call a real, paid model. The amounts are small, but they are real, so
sign out or remove deployments you are not using. Several chapters use preview
packages: the native Foundry path (Chapter 3), the in-memory vector store
(Chapter 6), the Azure Monitor exporter (Chapter 14), the evaluation libraries
(Chapter 15), and the A2A protocol and hosting integration (Chapter 16). Each is
flagged in the relevant chapter and pinned to an exact version in
Directory.Packages.props.
