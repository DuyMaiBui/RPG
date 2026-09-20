---
name: unity-di-modules
description: Design and implement reusable Unity C# modules with VContainer DI, engine-independent gameplay core and minimal MonoBehaviour bridges.
---

# unity-di-modules

Read [the project architecture policy](../../../Docs/Architecture.md). Identify the existing module owner and reuse it before adding a layer.

- Keep public contracts narrow and consumer-owned. Plain C# implementations receive dependencies through constructors. Restrict VContainer and LifetimeScope to composition/integration.
- Keep domain assemblies free of UnityEngine, LitMotion and container APIs. Application services may use UniTask if async is intrinsic; deterministic simulation does not need it.
- MonoBehaviour holds scene references, forwards Unity callbacks, and applies output. Put gameplay decisions in core services; adapt physics/input behind ports where needed.
- Make assembly dependencies explicit and acyclic. Use noEngineReferences for genuinely pure domain assemblies. Test the exported API from a second consumer before advertising a module as reusable.
- Assign cancellation/disposal ownership to scene/session/operation scopes; do not capture shorter-lived services in longer-lived ones.
- Use interfaces at real substitution or module boundaries; avoid speculative factories, framework wrappers and duplicate abstractions over VContainer.

Confirm package presence and API versions before implementation. See [VContainer](https://vcontainer.hadashikick.jp/) for source documentation.
