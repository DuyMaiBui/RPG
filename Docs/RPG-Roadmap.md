# RPG game roadmap — phases, gates and status

This file tracks the agreed direction for the **RPG game** (not the physics
kernel) and the concrete remaining work, so a fresh session can pick it up.
It mirrors `Docs/AuraEngine/Roadmap.md`.

Three plans exist in this repository and must not be confused:

| Plan | Scope |
|---|---|
| `Docs/AuraEngine/Roadmap.md` | the C++ physics kernel `Native/AuraEngine` |
| **this file** | the RPG game built on `RPG.Core` + `RPG.Simulation`: phases, gates, status |
| `Docs/RPG-Combat-Plan.md` | what combat **is**: model, feature set, data schema, milestones C1–C8 |

Phase order here wins; the combat plan is the specification for the combat parts
of phases 2, 4, 6 and 7.

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
| 1 | Composition root + authored content pipeline | **Done** |
| 2 | Playable loop: player agency + battle HUD | **In progress** — C1a orders done |
| 3 | Progression, stats and persistence | Open |
| 4 | Meta structure: campaign, encounters, roster | Open |
| 5 | Presentation and feel | Open |
| 6 | AI and tactical depth | Open — C6 movement recovery landed early |
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
  `TurnBattleDemo.unity` — renamed to `SkirmishBattleDemo.unity` under
  decision 8 — and `StressBattleDemo.unity`.
- **Tests**: after phase 1, C1a, the movement fix, the scenario harness and the
  melee content change, `Assets/Tests/EditMode` holds **386** tests = **149** in
  `Core.Tests` + `Simulation.Contracts.Tests` (engine-free and
  headless-runnable), **13** in `Unity.Tests` (Editor-only, authored-asset and
  loadout validation), and **224** in `Assets/AuraEngine/Tests` (out of scope
  for this plan). `Assets/Tests/PlayMode` holds one `[UnityTest]`
  (`NavigationStressPlayModeTests`). The standalone harness at `/tmp/rpg-tests`
  compiles the engine-free sources and runs the 149 headless through
  `dotnet test` (needs `DOTNET_ROLL_FORWARD=LatestMajor` against this machine's
  .NET 9 runtime). Seven further tests are `[Explicit]` measurement/known-defect
  runs and are excluded from the suite counts. Last verified: **EditMode
  386/386**, **headless 149/149**, **PlayMode 1/1**, plus a Play session of the
  Moba demo with the melee health multiplier read back from the scene
  (`Melee x2`, `Projectile x1`) and a clean console.

### Phase-0 defects: status

1. **The turn-based path was inert — resolved by removal (decision 4).**
   `RpgSimulationApplication.CreateUpdate` always passed
   `activeActorId: EntityId.None`, and `TurnBattleDemoDriver.OnFrame` returned
   immediately when `frame.ActiveActorId.IsNone`, so `AttackCommand`,
   `TurnState` and the driver never executed in a running scene. `TurnState`,
   `AttackCommand` and `TurnBattleDemoDriver` are deleted, the driver component
   is removed from all three demo scenes, and `WorldFrameUpdate.Round` /
   `TurnNumber` / `ActiveActorId` are now always-default fields with no
   producer — phase 2 must either populate them for the result/turn UI or drop
   them from the frame contract.
2. **Player input is nearly inert — carried into phase 2.**
   `SimulationMoveInput` sends `MoveIntentCommand` for one mapped actor;
   `MobaBattleDemo` has no mapped player actor, so keyboard input does nothing
   there. Under the full-RTS decision this whole input path is replaced anyway.
3. **Empty assembly — resolved.** `Assets/Scripts/Combat/RPG.Combat.asmdef`
   declared an assembly with no sources and no references; it was deleted along
   with the now-empty `Assets/Scripts/Combat` folder.
4. **Protocol identity equality recursed — fixed (found in C1a).** `PlayerId`
   and `EntityId` implement `IEquatable<T>` explicitly by convention, and their
   `Equals(object)` called `Equals(other)`, which binds back to
   `Equals(object)`: any `id.Equals(someObject)` overflowed the stack. The order
   work hit it because `PlayerRoster` compares players. Both types now compare
   through one private core, expose `==`/`!=`, and `ProtocolIdentityTests`
   guards the contract.
5. **Dense crowds stalled mid-way — fixed (found while investigating a report of
   units stopping during pathfinding).** `CollisionResolver.SeparateCircles`
   resolved a full overlap every tick, so in a crowd a separation push (up to a
   full body diameter) dwarfed the actor's own step (0.013 units at speed 0.8)
   and shoved units back the way they came; nothing detected the lack of
   progress. Separation is now a bounded relaxation with a per-tick cap,
   an actor without headway for 45 ticks steps sideways for 20, and a cohort
   with no route steers straight at its destination instead of standing still.
   Measured: stalled units 42 → 16 (1-unit gap), 23 → 7 (1.5), 22 → 7 (2),
   10 → 2 (3). `CrowdMovementTests` guards it; details in
   `Docs/RPG-Combat-Plan.md` (C6).
6. **Basic attacks almost never fire — engine fixed, melee part is a content
   decision (measured 2026-10-06 on the Moba scenario run headlessly from its
   authored data).** 27 basic attacks against 591 ability casts over 300 s, and
   no melee unit (0 of 182) landed one. Three causes were fixed: a chase stopped
   exactly on its attack reach so the separation push left it out of range;
   only the assigned target could be attacked, so a unit pressed against an
   enemy chased something further away; and an actor avoided its own target and
   was blocked by the ranged rank in front of it. Ranged basic attacks went to
   82 per 300 s and a melee duel closes to contact. Melee basic attacks then
   stayed at zero because Cleave (1.92) and VenomStrike (4.72) outrange the 0.97
   melee attack, so the content now hardens the melee rank instead: a per-loadout
   `_healthMultiplier` (2 for melee, 1 for ranged, written into the three demo
   scenes) and Cleave damage 8 -> 4. Melee reaches contact (closest approach
   1.88 -> 0.86) and lands basic attacks; 27 -> 141 attacks per 300 s and no unit
   stalls any more. Ranged poison is still the dominant damage source.
   `MobaScenarioTests.Battle_BasicAttacksLand` guards it; evidence in
   `Docs/RPG-Combat-Plan.md` (C2).
7. **Units pinned on the obstacle field — fixed (measured 2026-10-06).** 16% of
   units used to never acquire a target, travel about eight units and then stand
   against the obstacle field for the rest of the match; with the obstacles
   removed that run pinned none, so the cause was the route/avoidance
   interaction at the terrain. A stuck actor now sweeps escape headings (each
   one checked against the navigation grid) with local avoidance bypassed while
   it escapes. Measured on the 300 s run: never acquired a target 42 → 16 of
   362, stalled for ten seconds or more 81 → 4, longest stall 132 s → 76 s.
   `MobaScenarioTests.Battle_UnitsAreNotPinnedOnTerrain` guards it; evidence in
   `Docs/RPG-Combat-Plan.md` (C6).

### Phase-0 baseline additions

`jp.hadashikick.vcontainer` 1.19.0 and `com.cysharp.unitask` 2.5.11 are now in
`Packages/manifest.json` and referenced by `RPG.Unity.asmdef`; the Editor
compiles clean with both.

The Moba demo battle can now be measured headlessly from its authored data:
`Assets/Tests/EditMode/Core.Tests/MobaScenarioData.cs` transcribes the scene map,
rules asset, content library and actor prefab into engine-free data, and asserts
a navigation fingerprint (`869631345`, 6353 blocked samples) taken from the
runtime grid so a wrong transcription fails. `MobaScenarioTests` runs the battle
in the ordinary suite; `MobaScenarioFlowDiagnostic` reports attack, movement and
stall numbers on demand and writes `/tmp/rpg_flow_report.txt`.

---

## Phase 1 — composition root and authored content pipeline

### Goal
Content becomes authored data with validation; the object graph gets one
explicit composition root instead of per-scene hand wiring.

### Problem (evidence, before this phase)
- Content lived in code (`Core/Actors/Abilities/AbilityCatalog.cs`, now deleted)
  or as Inspector fields on `BattleDemoBootstrap` / `ActorLoadoutAuthoring`;
  there was **no** `ScriptableObject` content asset anywhere under
  `Assets/Scripts`.
- `BattleDemoBootstrap.Start` constructs `RpgSimulationState`, the host, the
  registry, the clients and the views itself; each new scene copies that logic.

### Work
All items are done; see *as implemented* below.

1. **Composition root** in `RPG.Unity`: one bootstrapper that builds the
   session scope, registers the module implementations and disposes them in
   reverse order. Keep constructor injection in the domain; the container only
   lives in the composition layer (`AGENTS.md`, `Docs/Architecture.md`).
   Container: VContainer (decision 2), already in the manifest.
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
5. ~~Delete `Assets/Scripts/Combat/RPG.Combat.asmdef`~~ — done with the
   decision-4 removal (assembly and folder deleted).

Delivered so far: 2 (without `StatusEffectAsset`/`ActorLoadoutAsset`/
`EncounterAsset`), 3 (for abilities and archetypes), 4 (with `AbilityCatalog`
deleted outright), 5.

### Content schema (implementation contract)

Engine-free module `RPG.Content` (`noEngineReferences`, references `RPG.Core`
and `RPG.Simulation.Contracts`):

| Type | Responsibility |
|---|---|
| `ContentValidationError` | `readonly struct`: `Code`, `Message`, `Source`. |
| `ContentValidationResult` | Ordered `IReadOnlyList<ContentValidationError>` + `IsValid`. |
| `ContentCatalog` | Immutable: ability lookup by id, and the ordered ability list per `ActorArchetype`. |
| `ContentCatalogBuilder` | `AddAbility`, `AddArchetypeLoadout`, `Build`; rejects duplicate ids, unknown ability references, empty archetype loadouts, and abilities with no effects or non-positive magnitudes. |

Ability ids stay plain `int` (matching `AbilityDefinition.Id`) with a
documented stability rule — no wrapper type until something actually needs to
distinguish id kinds. Rules the builder enforces:

| Code | Rejected |
|---|---|
| `duplicate-ability-id` | the same id defined by two assets |
| `duplicate-archetype-loadout` | two loadouts for one archetype |
| `empty-archetype-loadout` | an archetype with no abilities |
| `unknown-ability-reference` | a loadout naming an id that no asset defines |
| `non-positive-magnitude` | any effect with magnitude ≤ 0 |
| `unexpected-duration` | `Damage`/`Heal` with a non-zero duration |
| `missing-duration` | a status effect (`Poison`/`Regeneration`/`Slow`/`Stun`) with duration ≤ 0 |

The asset layer adds `missing-ability-asset`, `missing-archetype-asset`,
`invalid-ability-id`, `invalid-ability-tuning`, `empty-ability-effects` and
`missing-ability-reference`, which the builder cannot see because an
`AbilityDefinition` cannot be constructed from them.

Unity-side authoring assets (in `RPG.Unity` under `Content/`):

| Type | Responsibility |
|---|---|
| `AbilityAsset : ScriptableObject` | `abilityId`, `cooldownTicks`, `range`, `targetMode`, `AbilityEffectAuthoring[]`. |
| `AbilityEffectAuthoring` | `[Serializable]` `(type, magnitude, durationTicks)`. |
| `ActorArchetypeAsset : ScriptableObject` | archetype + its `AbilityAsset[]` in cast-priority order. |
| `BattleRulesAsset : ScriptableObject` | tick rate, navigation grid, base offset/reach, vision, actor radius. |
| `ContentLibraryAsset : ScriptableObject` | the root: all ability/archetype assets; builds the `ContentCatalog` and surfaces `ContentValidationResult` errors in the Inspector. |

### Phase-1 content pipeline — as implemented

- `RPG.Content` is a `noEngineReferences` assembly under
  `Assets/Scripts/Content/`.
- Authoring assets live under `Assets/Scripts/Unity/Content/`:
  `AbilityAsset`, `AbilityEffectAuthoring`, `ActorArchetypeAsset`,
  `BattleRulesAsset`, `ContentLibraryAsset`, plus a `ContentLibraryAssetEditor`
  that renders the validation report inline.
- Shipped content is authored under `Assets/Content/`: five abilities (ids 1–5,
  preserving the previous `AbilityCatalog` values), three archetype loadouts
  (Bruiser/Skirmisher/Support) and three `BattleRulesAsset`s — Moba (60×40 grid),
  Stress (60×40, 100 per faction), Skirmish (20×10, 3 per faction).
- `AbilityCatalog` was **deleted**, not kept as a fixture: its definitions now
  exist only as assets. `AbilityCatalogTests` was replaced by
  `ContentCatalogBuilderTests` (13 rule/ordering tests) and
  `ContentCatalogAbilityTests` (2 end-to-end ability tests) in `Core.Tests`, plus
  `ContentLibraryAssetTests` (10 asset-level cases) in a new Editor-only
  `RPG.Unity.Tests` assembly.
- `ActorLoadoutAuthoring.CreateSpawnData` takes the `ContentCatalog`. A loadout
  also carries a `_healthMultiplier` applied to the session health, which is how
  the melee rank is made tougher than the rank that shoots from safety (the demo
  scenes use 2 for melee and 1 for ranged); a non-positive value is rejected.
  `ActorLoadoutAuthoringTests` pins both. `BattleSessionLifetimeScope` takes
  `battleRules` + `content` references, fails with the full validation report
  when the library is invalid, and the session reads tick rate, grid size,
  per-faction count and base offset from the rules asset.
- Wave composition is **not** content yet: it stays in `BattleSessionSetup` and
  moves to `EncounterAsset` in phase 4. Per-actor stats (health, power, move
  speed, vision, radius) likewise move in phase 3's stats pipeline.

### Phase-1 composition root — as implemented

- `BattleSessionLifetimeScope : LifetimeScope` is the scene's composition root.
  It keeps only serialized references and `Configure(IContainerBuilder)`, where
  it validates its authoring, registers the battle rules, the compiled catalog,
  the setup, the bridge instance and the view registry, and registers
  `BattleSession` as an entry point. VContainer creates and disposes it.
- `BattleSession : IStartable, IDisposable` is plain C# and owns one battle:
  it builds `RpgSimulationState`, applies the navigation obstacles, spawns the
  actors with their clients and views, starts the fixed-tick host, and on
  disposal releases the bridge, disposes the clients and disposes the host —
  in that order.
- `BattleSessionSetup` is the injected value object carrying the serialized
  scene references and the tuning that is not authored content yet, so the
  session never reads a MonoBehaviour.
- `ActorViewRegistry` owns view creation (`Create(id, faction, position, name)`)
  instead of the caller instantiating and registering views.
- The scope is a renamed `BattleDemoBootstrap` (script GUID preserved, so the
  three scenes kept every serialized reference; the scene object was renamed to
  match).
- Lifetime semantics come from VContainer, verified in the package source:
  container-created instances implementing `IDisposable` are tracked and
  disposed by `Container.Dispose()`, while `RegisterInstance` values (the scene
  bridge, the assets) are deliberately **not** disposed by the container.

### Acceptance
- A new ability or archetype loadout can be authored and played **without
  touching C#** — verified: all three demo scenes run from `Assets/Content`, and
  a wave schedule still needs C# until phase 4.
- The validator is exercised for every malformed case above — verified: 13
  builder cases run headless, 10 asset-level cases run in the Editor.
- No scene builds content by hand — verified: all three scenes reference
  `ContentLibrary.asset` and their own `BattleRulesAsset`.
- One composition root serves the session — verified: all three scenes hold a
  `BattleSessionLifetimeScope`, and no scene code constructs the graph.
- Session teardown releases the session — verified in Play mode: destroying the
  scope GameObject mid-battle dropped the actor-view count from 12 to 0, i.e.
  the container disposed `BattleSession`, which released the bridge and its
  views. No console errors in any Play session.

### Depends on
Nothing. This is the first phase to execute.

---

## Phase 2 — playable loop: player agency and battle HUD

### Goal
A human plays a battle start → victory/defeat with mouse and keyboard, instead
of watching auto-combat.

Combat scope here: **C1 orders and control**, **C2 aggro/stances**, **C3 damage
and mitigation**, **C4 ability model**, **C5 status model** — see
`Docs/RPG-Combat-Plan.md`.

### Progress

**C1a landed.** Per-unit order queues (`OrderQueueComponent`), the six order
commands (`MoveOrder`, `AttackMoveOrder`, `AttackOrder`, `CastAbilityOrder`,
`HoldOrder`, `StopOrder`), host validation that reports an `OrderRejected`
signal per refusal, `PlayerRoster` (one player owns many actors, replacing the
1:1 `PlayerActors` map), and order-driven movement, targeting and casting
including "walk into range first". Exact per-order semantics are in
`Docs/RPG-Combat-Plan.md`.

Still open in phase 2: C1b (selection, box-select, order input, selection
marker, and the deletion of the placeholder single-actor path), then C2–C5.

Verified: **EditMode 378/378**, **headless 144/144** (three consecutive runs),
**PlayMode 1/1**, and a clean `MobaBattleDemo` Play session with no console
errors.

### Problem (evidence, before this phase)
- The only player command was `MoveIntentCommand`, affecting movement of a
  single mapped actor (`SimulationMoveInput`) — now a placeholder next to the
  order commands, deleted in C1b.
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

Combat scope here: **C8 objectives/morale** and the `EncounterAsset` schema, plus
the ability and status content extensions — see `Docs/RPG-Combat-Plan.md`.

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

Combat scope here: **C6 formation and movement combat**, **C7 vision and
information**, and the AI that consumes them — see
`Docs/RPG-Combat-Plan.md`.

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

Combat scope here: the cost of vision recomputation, path requests,
targeting/threat evaluation and per-tick snapshot allocation — see
`Docs/RPG-Combat-Plan.md`.

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

## Decisions (settled 2026-10-06)

Recorded here so later phases do not reopen them.

1. **Game frame: full RTS.** The player controls every unit — selection,
   move/attack/ability orders per unit and per group — not a single
   auto-battling hero. Consequences: `ManualMovementComponent` +
   `MoveIntentCommand` as the only player input is a placeholder, not the
   target; phase 2 owes group selection, formation/hold orders from the player,
   and per-unit ability casting; phase 6 owes an order system that the player
   and the encounter script share.
2. **DI: VContainer, adopted now.** `jp.hadashikick.vcontainer` 1.19.0 is added
   to `Packages/manifest.json`. Registration lives in the composition layer
   only; domain services stay constructor-injected and container-free.
3. **Async: UniTask, added now.** `com.cysharp.unitask` 2.5.11 is added to
   `Packages/manifest.json`. Phase 2's scene/session flow uses it with explicit
   cancellation and ownership.
4. **Turn-based combat: removed.** The turn pipeline is dead code (see the
   phase-0 defects) and full RTS does not use it. `TurnState`, `AttackCommand`
   and `TurnBattleDemoDriver` are deleted; the attack path is the real-time
   `AttackComponent`/`AutoBattleSystem` one plus the phase-2 `AttackTargetCommand`.
5. **AuraEngine boundary: separate, permanently for now.** No heavyweight
   physics feature is in RPG scope.
6. **Save authority: local-only.** Saves are client-side for phases 3–8; if
   phase 9 happens, server validation is a new requirement, not an assumption
   baked into the phase-3 schema.
7. **Combat model: settled in `Docs/RPG-Combat-Plan.md`** (accepted
   2026-10-06). Real time with pause and speed control; per-unit order queues
   with stances and rules of engagement; typed, mitigated integer damage with
   seeded-RNG crit/block/dodge; abilities with resource cost, cast and channel
   times, area shapes and ground targeting; status categories with stack
   policies and diminishing returns; ticked formations; per-faction vision with
   explored memory; morale/rout and objectives; a seeded simulation RNG is the
   only randomness. Combat milestones C1–C8 map onto roadmap phases 2, 4, 6, 7.
8. **Remaining scene/content loose ends, decided.** The `TurnBattleDemo` scene
   is renamed to `SkirmishBattleDemo` (it is a 3v3 auto-battle sandbox with no
   turns); wave composition stays on the bootstrap and becomes `EncounterAsset`
   in phase 4; per-actor stats stay on the bootstrap until phase 3's stats
   pipeline.

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
