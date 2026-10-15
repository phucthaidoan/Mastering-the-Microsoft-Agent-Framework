# Chapter 15: Logistics Evaluation

This MSTest project evaluates the Chapter 14 logistics-routing agent. One test
uses a deterministic NLP evaluator, and one uses an LLM judge for tool-call
quality.

Book listing map:
- LogisticsEvaluationTests.cs: deterministic F1 evaluation.
- LogisticsEvaluationTests.cs: LLM-judge tool-call accuracy evaluation.
- LogisticsEvaluationTests.cs: the copied logistics agent/tool setup from Chapter 14.

Run from the repository root:

    dotnet test ./src/Ch15.LogisticsEvaluation/Ch15.LogisticsEvaluation.csproj
