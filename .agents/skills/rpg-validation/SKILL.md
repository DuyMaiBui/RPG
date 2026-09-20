---
name: rpg-validation
description: Choose and execute proportionate Unity validation for changed C# logic, lifecycle, serialization, assets and visible gameplay, and report evidence.
---

# rpg-validation

Read project `AGENTS.md` first.

Identify changed behavior and the smallest checks that can disprove correctness.

- Pure logic: focused existing Edit Mode tests, plus a regression test for a meaningful newly fixed bug when needed.
- Lifecycle, physics or scene integration: Unity compilation and focused Play Mode checks.
- Save/config changes: round-trip, migration and missing/corrupt input cases relevant to the change.
- Visible changes: inspect the actual Game view or screenshot with the target scenario; syntax checks cannot establish appearance.
- AI configuration/docs only: validate skill frontmatter, reference links, TOML parsing and role-file paths; do not run gameplay tests for these alone.

Discover the project's actual test assemblies and commands. Consult the Unity CLI skill before invoking test/build commands. Reuse a running Editor appropriately; do not start competing automation against the same project. Before Editor-state changes in parallel work, obtain exclusive ownership from the main agent.

Record the command/check, result and scope. Distinguish passed, failed and not run with reasons. Compilation failures should be attributed from evidence rather than guessed. Do not repeat broader suites after relevant checks pass unless changes or failures justify it.

## Project architecture priority

Follow [the project policy](../../../Docs/Architecture.md): constructor-injected C# core, explicit reusable modules, thin MonoBehaviour bridges, UniTask with owned cancellation, and LitMotion only in presentation. Search existing implementations and installed APIs first. These requirements override generic upstream examples of service locators, coroutine-driven gameplay or monolithic MonoBehaviour managers.
