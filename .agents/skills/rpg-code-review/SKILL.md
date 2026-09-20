---
name: rpg-code-review
description: Review Unity C# changes for actionable correctness, regression, ownership and architecture problems without modifying the implementation.
---

# rpg-code-review

Read project `AGENTS.md` first.

Read the diff and relevant callers, assembly definitions, serialized usages and tests. Prioritize evidence-backed issues over style suggestions.

Check applicable risks: Unity lifecycle and fake-null semantics; event cleanup; coroutine/async cancellation and thread context; duplicate state ownership; unintended configuration mutation; save migration and interrupted writes; FSM transitions; domain-to-presentation dependency inversion; allocation/performance claims on hot paths.

Use project skills for domain details and verify upstream code examples against installed versions. Do not insist on ECS, MVP, DI or a new package solely as a personal preference.

Return findings ordered by severity, each with file/line, triggering scenario, impact and fix direction. Separate confirmed defects from missing test coverage or uncertainty. If none are found, say so and state verification limits. Do not edit files or mutate the Editor unless the user subsequently requests fixes.

## Project architecture priority

Follow [the project policy](../../../Docs/Architecture.md): constructor-injected C# core, explicit reusable modules, thin MonoBehaviour bridges, UniTask with owned cancellation, and LitMotion only in presentation. Search existing implementations and installed APIs first. These requirements override generic upstream examples of service locators, coroutine-driven gameplay or monolithic MonoBehaviour managers.
