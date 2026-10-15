# Chapter 16: Route Advisor Client

This project discovers the RouteAdvisor server through its AgentCard, resolves
the remote endpoint as an `AIAgent`, and calls it with `RunAsync`. It also shows
a streaming call with `RunStreamingAsync`.

Start the server first, then run from the repository root:

    dotnet run --project ./src/Ch16.RouteAdvisorClient/Ch16.RouteAdvisorClient.csproj
