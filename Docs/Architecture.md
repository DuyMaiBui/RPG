# Gameplay architecture policy

## Dependency direction

```text
Composition (VContainer registrations, lifetimes, entry points)
    -> Unity adapters / presenters (MonoBehaviour bridges, UniTask, LitMotion)
    -> Application orchestration (plain C#, async where needed)
    -> Domain / module contracts (plain C#, engine-independent)
```

Dependencies point inward. Domain defines ports it needs; adapters implement them. Composition connects implementations. A module may use fewer layers when it stays cohesive. Use current `Assets/Scripts` paths, with explicit `.asmdef` references when splitting code. `RPG.Core` must not become a miscellaneous dumping ground. Split a feature's domain and Unity adapter into separate assemblies when engine independence requires it; do not rename or migrate assemblies without checking serialized usages.

## DI and lifetime

Prefer constructor injection for plain C# services. Keep VContainer-specific interfaces and registrations outside reusable domain assemblies. Bridges may use injection methods. Restrict Resolve calls to composition or justified factory adapters; no global resolver, static service registry, singleton gameplay manager or scene searches as dependency acquisition.

Choose application, scene/session or operation lifetime explicitly. Longer-lived services must not capture shorter-lived scene objects. Cancel work and unsubscribe events before disposal releases dependencies. Pool return requires its own cancellation/reset; OnDestroy alone is insufficient. Prefer explicit ownership over automatic registration magic.

## Async and motion

Use UniTask for loading, transitions and application-level async flows. Pass caller cancellation through the chain, dispose owned CancellationTokenSource instances, distinguish expected cancellation from failure and observe exceptions. Do not await the same UniTask multiple times unless using an explicitly supported sharing mechanism. Only main-thread code touches Unity objects. Domain simulation remains synchronous, with no delay/tween deciding damage or cooldown authority.

LitMotion is the chosen tween API for visual feedback and transitions. Store handles where replacement/cancellation needs ownership. Dispose/cancel on view teardown, scope end and pool return. Use the installed version's UniTask integration and verify assembly/define requirements before referencing extension methods; do not invent overloads. Completion and cancellation are different outcomes. Avoid a second tween library or custom Update-based tween engine for existing LitMotion features.

## Reuse and validation

Search the repository, installed dependencies and official upstream APIs first. For a significant new dependency compare Unity version, license, maintenance, AOT/IL2CPP/platform support and real integration cost. New dependencies must solve the requested capability rather than hypothetical future work. Extract a UPM package only when ownership and consumers justify it.

Test domain behavior without scenes. Test adapter lifecycle, cancellation, pooling and rendering with appropriate Unity integration checks. For modular changes check dependency cycles, engine references and serialized compatibility. Minimal code is the smallest complete correct solution, not fewer files at the cost of coupling.

## References

- [VContainer lifetime](https://vcontainer.hadashikick.jp/scoping/lifetime-overview)
- [UniTask source and cancellation guidance](https://github.com/Cysharp/UniTask)
- [LitMotion source and documentation](https://github.com/annulusgames/LitMotion)

This file defines the target policy. `Packages/manifest.json` remains the source of truth for installed dependencies.
