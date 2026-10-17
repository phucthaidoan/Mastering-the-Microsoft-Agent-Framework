# Chapter 9: File-Based Policy Expert Skill

This project is a separate copy of the updated `Ch09.PolicyExpertSkill` sample.
It shows a file-based Agent Skill using Microsoft Agent Framework
1.6.1. A general assistant uses `AgentSkillsProvider` to discover the
`policy-expert` skill from `Skills/policy-expert/SKILL.md`.

Agent Skills are experimental in the pinned 1.6.1 package. `Program.cs` suppresses
`MAAI001` only around provider creation; no package upgrade is required.

The provider advertises the skill's name and description to the model. For a
workplace-policy question, the assistant calls the provider's `load_skill` tool
to read the instructions, then calls `LookUpPolicy` for the relevant policy text
before answering. Non-policy
questions are answered normally with a note identifying them as non-policy.
Unsupported policy topics are reported as not covered.

`SKILL.md` tells the model when and how to use `LookUpPolicy`. The tool is
registered on the same assistant through `ChatOptions.Tools`. The model selects
the calls, and the framework invokes the registered C# function. The skill file
does not execute the function itself or create a separate agent. These instructions
guide model behavior; they do not enforce a tool-call sequence in application code.

Book listing map:
- `Program.cs`: file-based skill provider, the assistant, the deterministic
  `LookUpPolicy` tool with invented policy data, and scripted or interactive questions.
- `Skills/policy-expert/SKILL.md`: YAML metadata and instructions for selecting
  policy topics, calling the tool, and answering from its results.
- `Ch09.PolicyExpertFileSkill.csproj`: copies the skill files to build and publish
  output. The provider resolves them relative to `AppContext.BaseDirectory`, so
  discovery does not depend on the working directory.

Scripted run from the repository root:

    dotnet run --project ./src/Ch09.PolicyExpertFileSkill/Ch09.PolicyExpertFileSkill.csproj

Interactive run:

    dotnet run --project ./src/Ch09.PolicyExpertFileSkill/Ch09.PolicyExpertFileSkill.csproj -- interactive

The scripted questions cover annual leave, client meals, a non-policy math
question, and an unsupported office-parking policy. With the model configured,
check that the assistant loads the skill and calls `LookUpPolicy` for policy
questions, uses the returned policy limits, flags the math question as non-policy
without a policy lookup, and reports parking as not covered.
