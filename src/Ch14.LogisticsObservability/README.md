# Chapter 14: Logistics Observability

This project instruments a logistics-routing agent with OpenTelemetry and prints
the trace spans to the console. No cloud telemetry service is required.

Application Insights export is optional. To opt in, set
`APPLICATIONINSIGHTS_CONNECTION_STRING` in the shell before running the project.

Book listing map:
- Program.cs: console tracer provider setup.
- Program.cs: the shipment lookup tool and invented shipment data.
- Program.cs: the `UseOpenTelemetry(...)` agent middleware.
- Program.cs: scripted and interactive runs.

Scripted run from the repository root:

    dotnet run --project ./src/Ch14.LogisticsObservability/Ch14.LogisticsObservability.csproj

Interactive run:

    dotnet run --project ./src/Ch14.LogisticsObservability/Ch14.LogisticsObservability.csproj -- interactive
