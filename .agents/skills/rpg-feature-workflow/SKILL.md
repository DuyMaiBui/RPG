---
name: rpg-feature-workflow
description: Plan and implement a bounded RPG Unity gameplay feature or cross-module refactor from concrete acceptance criteria through validation.
---

# rpg-feature-workflow

Read project `AGENTS.md` first.

1. Identify the requested player-visible outcome and a minimal acceptance scenario. Inspect existing code, assembly references, package versions and dirty work.
2. For cross-module work, sketch state ownership, data flow and Unity adapters. Keep the design proportional; proceed with reasonable reversible choices when the request is clear.
3. Route architecture, FSM, configuration, persistence and async details to the relevant installed Unity skill. MVP is an option for UI; data-oriented layout should follow access patterns and profiling rather than an assumed ECS migration.
4. Implement a small end-to-end slice using existing paths. Preserve GUIDs and serialization compatibility. Avoid framework or dependency installation unless the task requires it.
5. Validate the acceptance scenario using rpg-validation. Report changed behavior, evidence and any unresolved limitation.

If parallel work is explicitly requested, give independent roles bounded scope and file ownership; serialize Editor mutations. This skill does not require delegation or extra approval gates.

## Project architecture priority

Follow [the project policy](../../../Docs/Architecture.md): constructor-injected C# core, explicit reusable modules, thin MonoBehaviour bridges, UniTask with owned cancellation, and LitMotion only in presentation. Search existing implementations and installed APIs first. These requirements override generic upstream examples of service locators, coroutine-driven gameplay or monolithic MonoBehaviour managers.
