# Chapter 13: Transaction Guardrails

This project shows two middleware layers on one transaction-monitoring agent: an
agent-run guardrail for deterministic input and output checks, and a function-call
guardrail around the transaction lookup tool.

Book listing map:
- Program.cs: the transaction lookup tool and invented data.
- Program.cs: the agent-run guardrail attached with `AsBuilder().Use(...)`.
- Program.cs: the function invocation guardrail attached with `Use(...)`.
- Program.cs: the scripted run that exercises pass, block, and redact paths.

Run from the repository root:

    dotnet run --project ./src/Ch13.TransactionGuardrails/Ch13.TransactionGuardrails.csproj
