# Chapter 9: Policy Expert Skill

This project shows a reusable capability packaged as an agent-backed function
tool. A focused policy expert agent is wrapped with `AsAIFunction()` and given to
a general assistant as the skill it can call for workplace-policy questions.

Book listing map:
- `Program.cs`: policy lookup data, the policy expert agent, the main assistant,
  and the scripted questions.
- `LookUpPolicy`: grounds the inner policy expert in deterministic local policy
  text.

Scripted run from the repository root:

    dotnet run --project ./src/Ch09.PolicyExpertSkill/Ch09.PolicyExpertSkill.csproj

Interactive run:

    dotnet run --project ./src/Ch09.PolicyExpertSkill/Ch09.PolicyExpertSkill.csproj -- interactive
