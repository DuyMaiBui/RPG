---
name: unity-async-patterns
description: Review and fix Unity C# asynchronous code and coroutines, cancellation, lifetime, thread switching, concurrent results and resource ownership.
---

# unity-async-patterns

## Project integration

- Read applicable project instructions, `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, and relevant code before choosing an implementation.
- Follow the chosen stack: VContainer DI, UniTask asynchronous integration and LitMotion presentation. Confirm installed versions before using APIs. Keep gameplay core C# engine-independent; MonoBehaviour is only a Unity bridge. See [project architecture](../../../Docs/Architecture.md).
- Use the existing `unity` MCP connection or Unity CLI for Editor operations. Do not add another Unity MCP server or bridge.
- Upstream mentions of Claude and other skills are optional topic references, not required tools or installed dependencies. Use available Codex capabilities and official documentation where needed.

## Workflow

Use UniTask for new Unity-facing async orchestration and adapt existing Task/Awaitable API boundaries explicitly. Keep the synchronous gameplay core free of player-loop async dependencies. Verify Awaitable, Task and UniTask APIs against installed versions; do not assume Awaitable.AsTask exists. Use supported Unity lifecycle signatures with an explicit async entry point and exception handling. Treat coroutines started with StartCoroutine as independently owned: yielding their handle does not provide structured cancellation; track and stop them explicitly. Check cancellation and object lifetime before applying results, and release owned resources on success, failure and cancellation.

Read [the upstream guide](references/upstream-guide.md) only for the relevant decision or pattern; follow its relative links for deeper examples. The source targets Unity 6.3 and contains illustrative scaffolds, not verified production code. Confirm API availability and adapt examples to the project's Editor and packages before using them. Validate changed gameplay behavior with appropriate Unity compile checks or focused tests.

## Source

Adapted for project-local Codex use from https://github.com/Nice-Wolf-Studio/unity-claude-skills/tree/a149d3e995420432f52b0899092daa4b069f441f/skills/unity-async-patterns.
Original guidance and supporting references are retained under `references/`; see [LICENSE](LICENSE). The project wrapper clarifies tool compatibility and corrects known example assumptions; it is not a complete audit of the upstream examples.

## Project architecture priority

Follow [the project policy](../../../Docs/Architecture.md): constructor-injected C# core, explicit reusable modules, thin MonoBehaviour bridges, UniTask with owned cancellation, and LitMotion only in presentation. Search existing implementations and installed APIs first. These requirements override generic upstream examples of service locators, coroutine-driven gameplay or monolithic MonoBehaviour managers.
