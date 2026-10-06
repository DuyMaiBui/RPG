# RPG game roadmap — phases, gates and status

This file tracks the agreed direction for the **RPG game** (not the physics
kernel) and the concrete remaining work, so a fresh session can pick it up.
It mirrors `Docs/AuraEngine/Roadmap.md`.

Two plans exist in this repository and must not be confused:

| Plan | Scope |
|---|---|
| `Docs/AuraEngine/Roadmap.md` | the C++ physics kernel `Native/AuraEngine` |
| **this file** | the RPG gameplay built on `RPG.Core` + `RPG.Simulation` |

## Agreed direction (already decided — do not relitigate)

- **Authoritative simulation in engine-free C#.** `RPG.Core`,
  `RPG.Simulation.Contracts` and `RPG.Simulation.Runtime` are
  `noEngineReferences` assemblies. Gameplay rules never reference Unity.
- **`RPG.Unity` is presentation + composition only**: MonoBehaviours are thin
  bridges for serialized references, input callbacks and rendering
  (`Docs/Architecture.md`, `AGENTS.md`).
- **Collision is the lightweight deterministic 2D layer** in
  `RPG.Core.Physics`, *not* AuraEngine. AuraEngine stays a separate experiment
  and may at most back one heavyweight feature (ragdoll, destructible) behind
  its own interface — it must not become the RPG core
  (`Docs/Collision-Architecture.md`).
- **All simulation math goes through `SimulationMath`**, never `MathF`
  directly, so a fixed-point backend stays a contained change.
- **Commands express intent, never state.** `AttackCommand(Target)` is valid;
  `SetHealthCommand` is not. The host owns authority (`Docs/Simulation-Architecture.md`).
- **Runtime never builds the view.** Serialized prefab/scene references are the
  source of truth for hierarchy, components, materials and UI; a missing
  reference is an authoring error to report, not to repair at runtime.
- **One declared type per file**, filename matches the type; interface members
  are implemented explicitly (`AGENTS.md`).

## Phase status

| Phase | Goal | Status |
|---|---|---|
| 0 | Deterministic battle vertical slice | **Done** |
| 1 | Composition root + authored content pipeline | Open |
| 2 | Playable loop: player agency + battle HUD | Open |
| 3 | Progression, stats and persistence | Open |
| 4 | Meta structure: campaign, encounters, roster | Open |
| 5 | Presentation and feel | Open |
| 6 | AI and tactical depth | Open |
| 7 | Scale, performance and validation | Open |
| 8 | Authoring and live-ops tooling | Open |
| 9 | Remote play (deferred) | Open |
| 10 | Shipping (platform, options, telemetry) | Open |

Phases 1–2 are the critical path: without a composition root and a content
pipeline, every later phase pays interest on hand-wired scenes and code
constants. Phases 3–4 can proceed in parallel once 1 lands. Phase 9 must not
start before phase 3 defines what state is authoritative vs. saved.

---

## Phase 0 — deterministic battle vertical slice (DONE)

Evidence in this repository:

- **Actor model**: `Actor`, `ActorComponentSet`, `ActorRegistry`,
  `ActorSpawnData`, `FactionId`, `ActorKind`; components for health,
  position, movement, manual movement, vision, target/target-priority, attack,
  attack cooldown, attack range, body, collider, faction, status effects,
  projectile weapon, movement cohort, abilities (`Core/Actors/Components`).
- **Combat**: `AutoBattleSystem` (waves, projectiles, spatial hash, occupancy,
  targeting, movement, attacks), `AbilitySystem` (auto-cast with range + line of
  sight), `StatusEffectSystem`, `CombatBehaviorTree`, `WaveSpawner`,
  `BattleResult`.
- **Content today**: `AbilityCatalog` (code constants) — `Cleave`,
  `VenomStrike`, `Mend`, `Hamstring`, `ConcussiveBlow`; archetypes
  `ActorArchetype.{Bruiser,Skirmisher,Support}`.
- **Movement**: `NavigationGrid`, `AStarPathfinder`, `NavigationFlowFieldCache`,
  `DynamicOccupancyGrid`, `MovementCohortCoordinator`, `OrcaAvoidanceSolver`,
  `SpatialHash`, `FormationCoordinator`.
- **Projectiles**: `Projectile`, `ProjectileRegistry`, `ProjectileSystem`.
- **Simulation**: `SimulationHost` (dedicated thread, fixed tick),
  `LocalSimulationClient`, `ClientCommandEnvelope`/`ServerUpdateEnvelope`,
  `PredictionBuffer`, `BehaviorTree` runtime, `RpgSimulationApplication`.
- **Unity**: `ActorView` + `ActorHealthView`/`ActorMovementView`/
  `ActorFactionView`/`ActorRangeView`/`ActorCombatFeedbackView`,
  `ProjectileView`, `HealthBarPresenter`, `FloatingCombatTextPool`,
  `SimulationUnityBridge`, `SimulationMoveInput`, `BattleDemoBootstrap`,
  collider authoring components.
- **Scenes**: `Assets/Scenes/MobaBattleDemo.unity`,
  `TurnBattleDemo.unity`, `StressBattleDemo.unity`.
- **Tests**: `Assets/Tests/EditMode` holds **94** tests — 11 test classes in
  `Core.Tests` (`AbilityCatalogTests`, `AbilityTests`, `ActorComponentSetTests`,
  `AutonomousCombatTests`, `CollisionQueryTests`, `CollisionResolverTests`,
  `NavigationLineOfSightTests`, `RpgSimulationTests`, `StatusEffectTests`,
  `StunTests`, `TargetVisibilityTests`) plus the `ThrowingApplication`
  fault-injection helper, and `Simulation.Contracts.Tests`. `Assets/Tests/PlayMode`
  holds one `[UnityTest]` (`NavigationStressPlayModeTests`). Last recorded run
  at `b7b0ca4`: **318/318** EditMode pass = these 94 + 224 from
  `Assets/AuraEngine/Tests` (out of scope for this plan). The standalone harness
  at `/tmp/rpg-tests` compiles the engine-free sources and runs the 94 without
  Unity via `dotnet test`.

### Known defects carried by phase 0 (fix inside phase 2)

1. **The turn-based path is inert.** `RpgSimulationApplication.CreateUpdate`
   always passes `activeActorId: EntityId.None`, and
   `TurnBattleDemoDriver.OnFrame` returns immediately when
   `frame.ActiveActorId.IsNone` — so `AttackCommand`, `TurnState` and
   `TurnBattleDemoDriver` never execute in a running scene. Either wire the
   turn pipeline through `WorldFrameUpdate.Round`/`TurnNumber`/`ActiveActorId`
   or delete `TurnState`, `AttackCommand` and the driver.
2. **Player input is nearly inert.** `SimulationMoveInput` sends
   `MoveIntentCommand` for one mapped actor; `MobaBattleDemo` has no mapped
   player actor, so keyboard input does nothing there.
3. **Empty assembly.** `Assets/Scripts/Combat/RPG.Combat.asmdef` declares an
   assembly with no sources and no references. Delete it (or give it a real
   module) in phase 1.

---

## Phase 1 — composition root and authored content pipeline

### Goal
Content becomes authored data with validation; the object graph gets one
explicit composition root instead of per-scene hand wiring.

### Problem (evidence)
- Content lives in code (`Core/Actors/Abilities/AbilityCatalog.cs`) or as
  Inspector fields on `BattleDemoBootstrap` / `ActorLoadoutAuthoring`; there is
  **no** `ScriptableObject` content asset anywhere under `Assets/Scripts`.
- `BattleDemoBootstrap.Start` constructs `RpgSimulationState`, the host, the
  registry, the clients and the views itself; each new scene copies that logic.

### Work
1. **Composition root** in `RPG.Unity`: one bootstrapper that builds the
   session scope, registers the module implementations and disposes them in
   reverse order. Keep constructor injection in the domain; the container only
   lives in the composition layer (`AGENTS.md`, `Docs/Architecture.md`).
   Decide the container (see *Decisions*).
2. **Content module** `RPG.Content` (engine-free) holding immutable records and
   ids; **authoring assets** in `RPG.Unity` (`ScriptableObject`) for
   `AbilityAsset`, `StatusEffectAsset`, `ActorArchetypeAsset`,
   `ActorLoadoutAsset`, `EncounterAsset` (faction composition + wave schedule),
   `BattleRulesAsset` (tick rate, nav grid, base positions, vision).
3. **Converter + validator**: authored assets → the existing domain records
   (`AbilityDefinition`, `AbilityEffect`, `ActorSpawnData`). One validation
   pass that reports duplicate ability ids, empty effect lists, non-positive
   magnitudes, missing collider shapes, out-of-range target modes and
   unreachable spawn points. Errors are visible in the Inspector and in an
   EditMode test.
4. **Catalog load**: `RpgSimulationState` receives the compiled catalog
   instead of reaching for `AbilityCatalog`. Keep `AbilityCatalog` as a test
   fixture only.
5. Delete `Assets/Scripts/Combat/RPG.Combat.asmdef` (dead assembly).

### Acceptance
- A new ability, archetype and wave schedule can be authored and played
  **without touching C#**.
- The validator fails an EditMode test on each malformed-asset case above.
- No scene constructs simulation state by hand anymore; one composition root
  serves all three demo scenes.

### Depends on
Nothing. This is the first phase to execute.

---

## Phase 2 — playable loop: player agency and battle HUD

### Goal
A human plays a battle start → victory/defeat with mouse and keyboard, instead
of watching auto-combat.

### Problem (evidence)
- The only player command is `MoveIntentCommand`, and only movement of a
  mapped actor is affected (`SimulationMoveInput`).
- Abilities are auto-cast only (`AbilitySystem`); there is no cast command, no
  targeting UI, no cooldown UI.
- No camera rig, no selection, no pointer picking: a search across
  `Assets/Scripts` for `class .*Camera|Selection|Pointer|Mouse|Physics2D`
  returns no matches. The only UI is `HealthBarView`/`HealthBarPresenter` and
  `FloatingCombatTextPool`.

### Work
1. **Command surface** (contracts + host validation): `CastAbilityCommand`
   (ability id + target entity + optional ground point), `AttackTargetCommand`,
   `StopCommand`, `HoldCommand`, `SelectHeroCommand`. Validation covers
   ownership, ability ownership, range, line of sight, cooldown and resource
   cost; a rejected command is observable, never silently applied.
2. **Input + adapters**: an Input System actions asset; pointer picking that
   maps a view collider back to `EntityId` (view→id only, never id→view
   mutation); an RTS-style camera rig (pan, zoom, follow, edge scroll) with
   LitMotion for focus transitions.
3. **HUD (authored prefabs, no runtime UI construction)**: hero frame
   (health, resource, level), ability bar with cooldown/charge state, target
   frame, wave/enemy counters, battle-result panel, pause/speed controls.
   Snapshots arrive through `SimulationUnityBridge`; the HUD never reads the
   simulation state directly.
4. **Session flow**: a state machine for menu → load → battle → result →
   retry/return, with async scene loading, explicit cancellation, and cleanup
   of views, tweens and subscriptions on every transition.
5. Fix the three phase-0 defects above.

### Acceptance
- A full battle is completed using only mouse and keyboard.
- An out-of-range or on-cooldown cast is rejected by the host and covered by
  an EditMode test.
- PlayMode test: five retry cycles leak no view, tween, subscription or host
  thread (assert registries/thread counts after each cycle).

### Depends on
Phase 1 (composition root; the session-flow state machine needs a scope owner).

---

## Phase 3 — progression, stats and persistence

### Goal
A run has progression, and both the run and the meta state survive a restart.

### Problem (evidence)
A search across `Assets/Scripts` for
`Save|Load|Persistent|JsonUtility|File\.|Serialize|Migration|Schema` matches
only `[SerializeField]` attributes and `ActorLoadoutAuthoring` (false
positives); `Inventory|Equipment|Loot|Drop|Experience|Progression|Unlock|Upgrade`
and `Random|Seed|Rng` return **no matches at all**. There is no persistence, no
item model, no progression curve and no seeded RNG anywhere in the gameplay
code.

### Work
1. **Stats pipeline**: base stats + ordered modifiers (equipment, status
   effects, buffs) evaluated at one point; extend `HealthComponent` and the
   attack components to read through it. Add a resource pool (mana/stamina)
   with costs and regeneration as a `StatusEffect`-consistent rule.
2. **Progression**: XP awards from kills/encounters, level thresholds, stat
   growth curves, ability unlocks gated by level.
3. **Items**: item definitions as content (slot, stat modifiers, granted
   ability), equipment slots, a deterministic seeded RNG service **inside the
   simulation** so loot is reproducible from a run seed and replayable.
4. **Persistence**: versioned save schema, atomic write (temp file + fsync +
   rename), corruption recovery that falls back to the last good save,
   explicit migration functions per version bump. Separate *run state*
   (in-progress battle) from *meta state* (progression, inventory, campaign
   progress) so a corrupt run never destroys meta progression.
5. Save/load integrates with the simulation boundary: the save is derived from
   authoritative state, and loading reconstructs the state through the same
   spawn path as a fresh run (no bespoke deserialization of live objects).

### Acceptance
- Same seed + same commands ⇒ identical loot (EditMode, deterministic).
- Save → quit → resume restores identical health, position, cooldowns,
  inventory and campaign progress.
- A v1→v2 migration test loads a checked-in v1 fixture.
- A truncated/corrupt file recovers meta state and reports the loss.

### Depends on
Phase 1 (content ids are the save keys).

---

## Phase 4 — meta structure: campaign, encounters, roster

### Goal
Battles belong to a structure: a sequence of encounters with objectives,
rewards and a roster the player builds.

### Problem (evidence)
A search across `Assets/Scripts` for
`Campaign|Encounter|LoadScene|SceneManager|Addressables|LoadingScreen` returns
no matches. The only `Objective` symbols — `MovementCohort.ObjectiveKey` and
`MovementCohortCoordinator.TryGetObjective` — are cohort destinations, not
battle objectives. `BattleResult` only distinguishes ongoing/win/loss for
annihilation, and three separate demo scenes carry three one-off setups.

### Work
1. **Objectives**: extend `BattleResult` scoring beyond annihilation —
   survive-N-ticks, destroy-target, escort, capture point. Objectives evaluate
   in a dedicated simulation step and are content-driven.
2. **Encounters as content**: `EncounterAsset` composes factions, wave
   schedules, spawn points, objectives, difficulty modifiers and reward tables;
   `WaveSpawner` consumes it instead of Inspector scratch fields.
3. **Campaign**: a node graph of encounters with unlock rules, difficulty
   scaling and persistent rewards; saved as meta state (phase 3).
4. **Roster**: hero selection, party composition, out-of-battle upgrades and
   loadout editing; all of it written to meta state.
5. **Scene flow**: one battle scene parameterised by encounter content,
   replacing the three demo scenes as the shipping path (demo scenes may
   remain as developer sandboxes).

### Acceptance
- A five-encounter campaign is playable end to end with progress surviving a
  restart.
- Each objective type passes a deterministic EditMode test.

### Depends on
Phases 1 and 3.

---

## Phase 5 — presentation and feel

### Goal
The battle reads correctly: animation, VFX, audio, feedback and readable UI.

### Problem (evidence)
A search across `Assets/Scripts` for
`AudioSource|AudioClip|ParticleSystem|VisualEffect|Animator|PlayEffect` returns
no matches. `ActorCombatFeedbackView` tints by state and `HealthBarPresenter`
animates via LitMotion; status is communicated by tint only, and there is no
animation, no VFX and no audio anywhere in the gameplay code.

### Work
1. **Animation**: actor views drive `Animator`/sprite animation from
   simulation state (one-way: state → presentation). Locomotion, attack,
   cast, hit, death. No animation event may grant damage, resources or
   cooldowns.
2. **VFX/SFX**: authored, pooled effect prefabs triggered by
   `PresentationSignal` (`AttackStarted`, `Damaged`, `AbilityCast`, `Died`);
   audio for hits, casts, deaths and UI. Cancel motion, tweens and audio on
   pool return, not only on destroy.
3. **Readability**: status icons instead of tints, floating damage types
   (physical/magic/heal), cast bars, telegraphs for enemy abilities,
   nameplates, camera shake on impactful hits.
4. **UI transitions** with LitMotion and explicit handle ownership; visual
   completion never drives authoritative state.
5. **Performance discipline**: no per-frame allocations in views; pooled views
   reuse components rather than rebuilding hierarchies.

### Acceptance
- A visual/audio checklist passes in a Play session (each event kind has a
  visible and audible response).
- A PlayMode cycle test asserts no leaked tweens, subscriptions or instantiated
  effect objects after pooling.

### Depends on
Phase 2 (signals and pooled views).

---

## Phase 6 — AI and tactical depth

### Goal
Enemies and allies behave tactically rather than uniformly charging.

### Problem (evidence)
`CombatBehaviorTree` is a 37-line selector mapping target/range to
`AutoCombatState`; `AbilitySystem` casts the first ready ability in catalog
order; there is no role, aggro, retreat, focus-fire or ability-scoring logic.

### Work
1. **Behavior trees** (the runtime already exists in
   `RPG.Simulation.Runtime/Client`): per-archetype trees with role selection,
   aggro/threat, focus fire, retreat at low health, kite for ranged, and
   ability selection by a scoring function (value vs. range vs. cooldown vs.
   resource) instead of catalog order.
2. **Squad orders**: hold / advance / retreat / formation per cohort,
   building on `MovementCohortCoordinator` and `FormationCoordinator`; orders
   are commands from the player or the encounter script, so they flow through
   the same authority path.
3. **Encounter scripting**: content-driven spawn triggers, reinforcements and
   boss phases as data, not as new C# per encounter.
4. **Pathing quality**: cohort spacing, stuck detection and re-pathing.

### Acceptance
- Each behavior has a deterministic EditMode scenario asserting the chosen
  action for a fixed world state.
- A demo encounter is observably different per archetype (recorded evidence).

### Depends on
Phases 1 and 4 (content-driven encounters).

---

## Phase 7 — scale, performance and validation

### Goal
Known unit counts hold the tick budget with measurable, regression-guarded
cost.

### Problem (evidence)
`StressBattleDemo` and `NavigationStressPlayModeTests` exist, but there is no
tick-time budget, no allocation budget and no perf gate recorded for gameplay
(only the AuraEngine kernel has one).

### Work
1. **Targets**: define unit counts (e.g. 100 / 300 / 1000 actors) and the tick
   budget per count at the shipping tick rate.
2. **Profiling harness**: headless run of `SimulationHost` with a scenario file
   measuring tick time, allocations and GC pressure; results recorded in this
   doc.
3. **Optimisations only where measured**: spatial hash/occupancy rebuild cost,
   snapshot allocation per frame, `PresentationSignal` churn, target selection
   cost.
4. **Soak**: long run with waves, checking for drift, leaks and determinism
   (state hash over a fixed command log).
5. **CI gate**: fail on a tick-time or allocation regression beyond a recorded
   threshold.

### Acceptance
- Documented tick time and allocation figures at each unit count, on named
  hardware.
- A determinism test replays a fixed command log and matches the state hash.

### Depends on
Phases 2–6 (measure the real game, not a synthetic loop).

---

## Phase 8 — authoring and live-ops tooling

### Goal
A designer can add and tune content without engineering, and a bug can be
reproduced from a recording.

### Work
1. **Editor windows**: content browser, validator report, balance table
   (abilities/stats/encounters side by side), bulk edit and duplicate.
2. **Content lint in CI**: the phase-1 validator runs headlessly over all
   assets on every commit.
3. **Debug overlay**: live simulation state inspector, command log, per-system
   tick timing, spawn/kill tools.
4. **Replay**: record the command log + seed, replay deterministically,
   scrub and inspect.

### Acceptance
- A designer authors a new encounter and ability with engineering absent.
- A recorded battle replays to an identical state hash.

### Depends on
Phases 1, 3, 7.

---

## Phase 9 — remote play (deferred)

Do **not** start before phases 1–4 settle what is authoritative versus saved.

The contracts already carry the reserved fields (`ProtocolVersion`,
`ClientSequence`, `ClientTick`, `ServerTick`, last-processed sequence,
server-owned `PlayerId`/`SessionId`), so the following are transport work only
(`Docs/Simulation-Architecture.md`):

1. Remote `ISimulationClient` implementation replacing `LocalSimulationClient`.
2. Session gateway: authentication, session join, rate limits, reconnect.
3. Client prediction + reconciliation beyond the current single-actor
   `PredictionBuffer`.
4. Delta replication, baseline negotiation and interest management.
5. Server deployment, persistence and observability.

### Acceptance
Two clients over a real network play the same battle with server authority and
no client-visible divergence after reconciliation.

---

## Phase 10 — shipping

Build pipeline and platform settings; options (audio, language, controls,
accessibility); localization; telemetry and crash reporting; store packaging;
save-data migration policy for post-launch patches.

---

## Decisions required before phase 1 starts

These materially change the plan; each has a recommended default.

1. **Game frame.** What is the player's role — a single controlled hero inside
   an otherwise auto-battling squad (recommended: matches the existing
   `PlayerActors` mapping, `ManualMovementComponent` and auto-cast systems) or
   full RTS control of every unit? Shapes phases 2 and 6.
2. **DI container.** Adopt VContainer (named as preferred in `AGENTS.md`) or
   keep explicit constructor wiring in the composition root? VContainer is
   **not** currently in `Packages/manifest.json`. Recommended: explicit wiring
   until a second lifetime scope (menu/battle session) actually exists, then
   adopt VContainer for the session scope only.
3. **Async package.** UniTask is named in `AGENTS.md` but is **not** in the
   manifest. Phase 2's scene flow needs cancellation and lifecycle handling;
   recommended: add UniTask when that flow is implemented, not before.
4. **Turn-based combat.** Keep the (currently dead) turn pipeline and fix it, or
   delete `TurnState`, `AttackCommand` and `TurnBattleDemoDriver`? Recommended:
   delete, unless turn-based is a shipping mode.
5. **AuraEngine boundary.** Is any heavyweight feature (ragdoll, destructible,
   vehicle) in the RPG scope? Recommended: no; keep AuraEngine separate until
   one concrete feature requires it.
6. **Save authority.** Local-only saves (recommended for phases 3–8) or
   server-validated saves from the start? Shapes phase 9.

## Verify loops

- Unity state first: `unity status --format json` (a missing instance is a
  connection problem, not a compile result).
- Compile: `unity command recompile`.
- EditMode: `unity command run_tests --mode EditMode --filter RPG.Core.Tests
  --filter_type assembly` (add `RPG.Simulation.Contracts.Tests`).
- PlayMode: `unity command run_tests --mode PlayMode --async_tests true`, then
  poll `test_status`.
- Headless core (no Unity, fast): the standalone harness at `/tmp/rpg-tests`
  compiles the engine-free sources and runs the 94 tests with `dotnet test`.
  It is outside the repository and does not survive a clean machine — recreate
  it (or promote it under `Tools/`) when it is needed again.
- Determinism/scale harness: add per phase 7; until then, ad-hoc headless runs.

## Known constraints

- The RPG simulation is deterministic **on one platform** (`float` math behind
  `SimulationMath`); bit-exact cross-platform replay is not a current
  requirement. Adopting fixed-point is a contained change behind the facade.
- The Editor is fragile under long Pipeline test runs; restart it when
  `editor_status` times out.
- `Assets/Scripts/AuraEngine` and `Native/AuraEngine` are out of scope for this
  plan; their roadmap is `Docs/AuraEngine/Roadmap.md`.
