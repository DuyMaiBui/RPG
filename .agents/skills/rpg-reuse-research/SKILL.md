---
name: rpg-reuse-research
description: Find and compare existing code and maintained Unity C# libraries before implementing a substantial gameplay subsystem or adding a dependency.
---

# rpg-reuse-research

Start with the actual requested behavior and constraints; do not research unrelated features.

1. Search project code and tests with rg, then manifest and installed package source. Identify existing ownership and reusable contracts.
2. Check whether Unity or the selected VContainer/UniTask/LitMotion stack already exposes the capability. Read actual API definitions for this project's versions.
3. For a substantial remaining gap, browse primary upstream repositories/documentation and compare a small number of relevant options for license, maintenance, Unity/AOT/IL2CPP/platform fit, dependencies and integration cost.
4. Choose reuse, a small adapter, a new dependency, or custom code. State the evidence and tradeoff briefly. Do not fabricate benchmarks or compatibility guarantees.
5. Implement the smallest complete solution in the correct module. Add meaningful validation proportional to the feature.

Ponytail supports this search-first habit. It does not justify removing requested DI/module boundaries, skipping cancellation or returning partial functionality. Preserve a single unity MCP connection.
