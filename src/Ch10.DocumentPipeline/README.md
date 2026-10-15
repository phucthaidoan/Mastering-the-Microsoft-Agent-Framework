# Chapter 10: Document Pipeline

This project demonstrates a sequential workflow: a deterministic extract step,
an agent-based summarize step, and a deterministic format step.

Book listing map:
- `Program.cs`: raw document input, workflow wiring, the extract executor, the
  summarize agent executor, the format executor, and the labeled run output.

Run from the repository root:

    dotnet run --project ./src/Ch10.DocumentPipeline/Ch10.DocumentPipeline.csproj
