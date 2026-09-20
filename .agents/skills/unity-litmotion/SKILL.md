---
name: unity-litmotion
description: Implement Unity presentation tweens with LitMotion and UniTask, explicit motion ownership, cancellation and pooling-safe lifecycle.
---

# unity-litmotion

Read [the architecture policy](../../../Docs/Architecture.md). Search existing presenters and installed LitMotion APIs before writing a new animation helper.

Use LitMotion in Unity adapters/presenters for position, scale, color, UI or feedback transitions. Keep animation details and MotionHandle out of gameplay contracts. Gameplay core decides outcomes; tween completion only coordinates presentation.

Store/cancel handles when an animation can be superseded. Stop work on pool return, disable or scope disposal according to the intended lifetime; destruction linkage alone is insufficient for pooled views. Avoid captured stale view references and avoid treating cancellation as successful completion.

For awaitable visual sequences use the installed LitMotion UniTask integration; inspect its source, package references and compilation defines rather than assuming a ToUniTask signature. Propagate cancellation from the application operation. Retain main-thread access for Unity bindings.

Do not introduce DOTween, LeanTween or a custom tween runner for capabilities LitMotion already provides. Avoid wrapping every LitMotion call in a new framework; extract a helper only for repeated domain-specific presentation behavior.

Validate completion, replacement, cancellation and pooled reuse for the changed behavior. [Primary source](https://github.com/annulusgames/LitMotion).
