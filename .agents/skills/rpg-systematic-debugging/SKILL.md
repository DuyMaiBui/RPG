---
name: rpg-systematic-debugging
description: Investigate reproducible Unity C# gameplay, lifecycle, scene-reference or asynchronous bugs and fix the evidenced root cause.
---

# rpg-systematic-debugging

Read project `AGENTS.md` first.

1. Capture the actual symptom, trigger, expected outcome and available log or stack trace. Try the smallest relevant reproduction; retain uncertainty if unavailable.
2. Trace the affected GameObject/prefab/serialized reference to the runtime script and mutating method. Check duplicate instances, execution order, event subscriptions and runtime overrides before changing nearby code.
3. Form a falsifiable hypothesis. Inspect the state or add narrow temporary diagnostics to distinguish it from alternatives. Change one causal variable at a time.
4. Fix the responsible behavior with minimal scope. For async issues inspect cancellation ownership, thread context, resource release and stale results with unity-async-patterns.
5. Reproduce again or run a meaningful regression test. Remove temporary diagnostics you introduced unless they are useful maintained logging. State clearly if runtime reproduction remains blocked.

Use only the existing unity MCP/Pipeline path. A failed connection does not authorize a second bridge or justify claiming the bug is fixed.

## Project architecture priority

Follow [the project policy](../../../Docs/Architecture.md): constructor-injected C# core, explicit reusable modules, thin MonoBehaviour bridges, UniTask with owned cancellation, and LitMotion only in presentation. Search existing implementations and installed APIs first. These requirements override generic upstream examples of service locators, coroutine-driven gameplay or monolithic MonoBehaviour managers.
