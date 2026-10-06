# RPG combat plan — real-time tactical combat on the deterministic simulation

This is the combat design and delivery plan for the RPG. `Docs/RPG-Roadmap.md`
owns *when* work happens (phases and gates); this file owns *what combat is*:
the model, the feature set, the data schema and the acceptance criteria for each
combat milestone. Where the two disagree, the roadmap's phase order wins and
this file's detail is the specification for that phase.

Decisions in this file are settled (accepted 2026-10-06). They are not reopened
without a concrete counter-example from a running battle.

## 0. Scope

In scope: unit and group control, targeting and aggro, a typed damage model,
abilities, status effects, formations, vision, morale, objectives, and the
combat-facing data schema — all inside the engine-free fixed-tick simulation,
presented by the Unity layer.

Out of scope (explicit): rigid-body physics (see
`Docs/Collision-Architecture.md`), naval/air layers, siege equipment,
destructible terrain, netcode (roadmap phase 9), and per-frame fog raycasting.

## 1. What combat is today (verified, with symbols)

Tick order inside `RpgSimulationApplication.Tick`:
`StatusEffectSystem` → `AutoBattleSystem` → `AbilitySystem`; commands are drained
by `SimulationHost` before `Tick`, events are collected after it, and
`CreateUpdate` projects `WorldFrameUpdate`.

| Area | Implemented | Symbol |
|---|---|---|
| Targeting | faction + vision range + `NavigationGrid.HasLineOfSight`, sticky until invalid | `AnyAliveEnemyTargetSelector.TrySelect`, `TargetComponent.CurrentTarget`, `TargetPriorityMode` = `Nearest`/`LowestHealth` |
| Attack | reach + integer cooldown, melee or projectile | `AttackComponent.AttackPower`, `AttackRangeComponent.Reach`, `AttackCooldownComponent`, `AttackType` = `Melee`/`Projectile` |
| Combat state | 3-state selector: acquire → attack → chase | `CombatBehaviorTree`, `AutoCombatState` = `AcquireTarget`/`ChaseTarget`/`AttackTarget`/`Dead` |
| Damage | single flat `int`, no types, no mitigation, no crit/block | `HealthComponent.ReceiveDamage`, `ActorDamaged.Source/Target/Damage` |
| Abilities | first-ready in loadout order, no cost, no cast time, two target modes | `AbilityComponent`, `AbilitySystem`, `AbilityTargetMode` = `CurrentTarget`/`Self`, `AbilityEffectType` = `Damage`/`Heal`/`Poison`/`Regeneration`/`Slow`/`Stun` |
| Status | 4 types, stack caps, periodic damage/heal, slow, stun | `StatusEffect.Type/Magnitude/RemainingTicks/Stacks`, `StatusEffectRules.MaxStacks/IsPeriodic/IsMovementModifier/IsDisabling` |
| Movement | cohorts (0.15 s rebuild) + ORCA avoidance + occupancy-aware grid resolution | `MovementCohortCoordinator`, `OrcaAvoidanceSolver`, `NavigationGrid.ResolveMovement`, `DynamicOccupancyGrid`, `SpatialHash` |
| Orders | **one** command, a direction vector, applied to one actor | `MoveIntentCommand.Direction` (TypeId 2), `RpgSimulationApplication.HandleCommand`, `PlayerActors` |
| Formations | data only — **not ticked** | `FormationCoordinator`, `FormationSlotComponent` |
| Vision | a per-actor range used for targeting | `VisionComponent.Range` |
| Frame | snapshots + signals + result | `ActorSnapshot`, `ProjectileSnapshot`, `PresentationSignal`, `PresentationSignalKind`, `BattleResult` = `Ongoing`/`RedWon`/`BlueWon`/`Draw` |
| Layers | filters exist but targeting ignores them | `ColliderFilter`, `ColliderMode`, `CollisionShapeType` |

The seven gaps that define this plan: no **orders** beyond a direction, no
**group** control, no **damage model**, no **cast/resource** model for abilities,
no **fog/visibility**, no **formation execution**, no **morale or objectives**.

## 2. Pillars

1. **Orders, not impulses.** The player issues intent; the simulation owns
   execution. An order survives until it completes, is replaced, or is
   impossible — never one frame of input.
2. **Position decides fights.** Facing, flanking, formation, chokepoints and
   spacing must be able to win or lose an engagement.
3. **Information is a resource.** What you can see shapes what you can target;
   scouting and hiding are real actions.
4. **Time is readable.** Cast times, wind-ups and cooldowns create the window to
   interrupt, dodge or commit — every one of them is telegraphed in the frame.
5. **Units have identity.** Archetype, ability kit, resource and veterancy make
   two units of equal health behave differently.
6. **Deterministic and replayable.** Fixed tick, no engine randomness; the only
   randomness is a seeded simulation RNG whose draws are part of the record.

## 3. Settled model decisions

**Time and authority.** 30 Hz fixed tick, host-authoritative, unchanged. Combat
is real time with pause and 0.5×/1×/2× speed control (single player); there is
no turn mode. Every player action is a validated `ISimulationCommand`; a
rejected order is reported, never silently dropped.

**Order model.** Each controllable actor has an order queue. An order is
`(kind, destination | target, formation, stance, queued)`. Kinds: `Move`,
`AttackMove`, `AttackTarget`, `CastAbility`, `Stop`, `Hold`, `Patrol`,
`SetStance`, `SetFormation`, `Follow`, `Retreat`. Shift-queue appends; a new
order without shift clears the queue. Stances: `Aggressive` (chase within leash),
`Defensive` (engage only inside a hold radius), `HoldPosition` (never leave the
current cell), `StandGround` (never move; attack what is in reach).

**Targeting and aggro.** Explicit targets always win for their duration. Without
one, auto-engagement uses `TargetPriorityMode` extended with `ClosestThreat`,
`LowestHealth`, `HighestThreat`, `WeakestArmor`. Threat is accumulated by
damage dealt and healing done, decays per tick, and is the tank's taunt anchor.
A leash radius sends a unit back to its post when the fight drags it too far.

**Damage.** Typed and mitigated, integer throughout:

```text
raw      = abilityOrAttackPower * (1 + sum(offensive modifiers))
mitigated= max(minimumDamage, raw - effectiveArmor(rawType))
effectiveArmor = armor * (1 - penetration)
final    = mitigated * critMultiplier   // crit drawn from the seeded sim RNG
```

Types: `Physical`, `Fire`, `Frost`, `Lightning`, `Poison`, `Holy`, `Shadow`,
`True` (ignores armor). Every actor carries armor plus per-type resistance
percentages; `True` bypasses both. Block and dodge are defender stats rolled
against the seeded RNG and reported in the frame so the HUD can show them.
Armor is never negative and mitigation is capped, so no combination reaches
zero or negative damage.

**Abilities.** Each ability declares: resource cost and resource type
(`Mana`/`Stamina`/`Rage`/`None`), cooldown ticks (and optional charges),
`CastTicks` (0 = instant, interrupted by damage or movement), `ChannelTicks`
(pulses while channelling, cancelled by damage, stun or a new order), area shape
(`SingleTarget`, `Self`, `Circle`, `Cone`, `Line`, `GroundCircle`), radius/angle,
whether it requires line of sight, and its effects. Ground targeting is a
position, not an entity. Effect kinds grow to: direct damage/heal, apply status,
dispel, summon, knockback, teleport/blink, shield absorb, resource drain,
taunt, revive.

**Status effects.** Reclassified into `Buff`, `Debuff`, `Control` (stun, root,
silence, disarm, slow, fear) and `DamageOverTime`. Stack policy per type:
`Refresh` (reset duration), `Stack` (independent instances up to a cap),
`Extend` (add duration). Control effects have a per-actor diminishing-returns
table (full → 50% → 25% → immune within a window) and immunity tags so a boss
cannot be chain-stunned. Damage can interrupt casts and channels.

**Formations and movement.** `FormationCoordinator` becomes a ticked system:
formations `Line`, `Column`, `Wedge`, `Box`, `Circle`, with spacing, facing and
a formation anchor; units path to their slot and re-slot when the anchor moves.
Cohorts keep handling crowd routing, ORCA keeps local avoidance, and unit
collision separation stays deterministic. Order-level movement is: path →
cohort route → avoidance → grid resolution → separation.

**Vision and information.** Per faction: `Visible` cells (sources: own units,
structures, temporary reveals) and `Explored` memory (geometry only). An enemy
outside every faction's vision is not targetable and is not projected in
`ActorSnapshot`. Stealth/hidden units are revealed by proximity or a reveal
effect. Vision is recomputed on a fixed cadence (not every frame) from a light
per-cell count, not per-pixel raycasting.

**Morale.** Units carry morale that falls from casualties nearby, being
flanked, low health and commander death; a broken unit routs toward the rally
point and is uncontrollable until it recovers or the battle ends. Heroes are
immune to rout; they can instead be `Downed` and rescued/revived.

**Objectives.** `BattleResult` gains objective evaluation beyond annihilation:
`Annihilate`, `Survive(tickLimit)`, `DestroyTarget(EntityId|tag)`,
`Capture(point, holdTicks)`, `Escort(entity, toPoint)`, `Defend(point, ticks)`.
Evaluation runs in its own tick step so the frame can carry progress.

**RPG coupling.** Combat produces what the RPG consumes: XP per kill and per
objective, loot rolls from the seeded RNG (roadmap phase 3), injuries that
persist between battles, and veterancy that grows unit stats. Downed heroes and
post-battle recovery are the bridge between a lost battle and campaign
progression.

## 4. Data schema (content additions)

Existing `Assets/Content` grows from abilities/archetypes/rules to:

| Asset | Owns |
|---|---|
| `AbilityAsset` (extended) | resource cost/type, cooldown, charges, cast/channel ticks, area shape + radius/angle, LoS requirement, effects, telegraph metadata |
| `StatusEffectAsset` (new) | category, stack policy, cap, duration, magnitude semantics, immunity tags, diminishing-returns group |
| `UnitStatAsset` (new) | armor, per-type resistances, crit chance/multiplier, block, dodge, morale, resource pool + regen |
| `ActorArchetypeAsset` (extended) | references `UnitStatAsset` + ability loadout + default stance and engagement radius |
| `FormationAsset` (new) | shape, spacing, facing, slot policy |
| `EncounterAsset` (new, roadmap phase 4) | factions, wave schedule, spawn points, objectives, difficulty modifiers, reward table |
| `BattleRulesAsset` (extended) | tick rate, grid, vision cadence, morale and diminishing-returns tuning |

All of it goes through `RPG.Content` validation: unknown ids, missing references,
negative or zero values where positive is required, empty loadouts, illegal
combinations (e.g. a `Cone` with radius 0, a channel with `ChannelTicks == 0`,
a stat block with resistances above the cap) are errors with stable codes, and
`ContentCatalogBuilder` reports every problem in one pass.

## 5. Combat milestones

Each milestone is a vertical slice: model in `RPG.Core`, commands in
`RPG.Simulation.Contracts`, content in `Assets/Content`, presentation in
`RPG.Unity`, and tests. Milestones map onto roadmap phases 2, 4, 6 and 7.

### C1 — Command and control surface (roadmap phase 2)
Per-unit order queue with `Move`/`AttackMove`/`AttackTarget`/`CastAbility`/
`Stop`/`Hold`; selection and box-select in the Unity layer; group order
fan-out to selected actors; shift-queued orders; formation-aware arrival.
Validation in the host: ownership, reachability, ability ownership, range, LoS,
cooldown, resource, stance legality.

**C1a — order model and command surface (done).** `OrderKind`, `ActorOrder`,
`OrderQueueComponent` (bounded, replace-vs-append, advance) and `PlayerRoster`
(one player owns many actors) live in `RPG.Core`; `OrderSystem` resolves the
queue once per tick before the auto battle; `MoveOrderCommand`,
`AttackMoveOrderCommand`, `AttackOrderCommand`, `CastAbilityOrderCommand`,
`HoldOrderCommand` and `StopOrderCommand` are the command surface, validated in
`RpgSimulationApplication.HandleCommand` with the rejection recorded as an
`OrderRejected` presentation signal. Ordered casting is resolved inside
`AbilitySystem` so cooldowns and the one-cast-per-tick rule stay in one place.

Semantics as implemented:

| Order | Acquires targets | Movement | Completes when |
|---|---|---|---|
| `Move` | no — clears the target | straight at the destination with local avoidance | within `OrderSystem.ArrivalRadius` |
| `AttackMove` | yes | chases an acquired enemy, otherwise the destination | arrived with nothing left to fight |
| `AttackTarget` | locked to the ordered target | chases to attack reach | target dead, gone or unseen |
| `CastAbility` | locked to the ordered target | chases to the ability's own reach | the ordered cast lands |
| `Hold` | yes, never chases | none | never — replaced or stopped |

Rejections are permanent problems only (`PlayerDoesNotOwnActor`, `ActorMissing`,
`ActorDead`, `UnknownAbility`, `TargetMissing`, `DestinationNotWalkable`,
`QueueFull`); an order that is merely blocked by range, line of sight or a
cooldown is accepted, and the actor walks into position or waits. While a cast
order is current it owns the actor's cast slot, so the ordered ability is never
silently replaced by an automatic one.

An ordered move steers straight at its destination rather than asking the
cohort system for a direction: cohort routes follow the battle objective (the
enemy base or a cohort's own target), which is not where the player pointed.
Local avoidance (ORCA), overlap separation and grid resolution still apply, and a
regression test covers a unit standing directly on the ordered path.

**C1b — selection and order input (open).** Click and box selection, order
issuing (right-click move, enemy click attack, ability and stance keys),
selection feedback and the authored selection marker; then the placeholder
single-actor path (`SimulationMoveInput`, `MoveIntentCommand`,
`ManualMovementComponent`, and the bridge's local-actor prediction) is deleted
in the same cutover.

*Acceptance:* a selected group moves, attacks and holds through orders only —
verified for the command/order layer by 32 deterministic tests plus a stable
headless suite (144 tests) and Unity EditMode (378 tests); the interactive part
is what C1b adds. An illegal order is rejected and observable in the frame —
verified. Deterministic tests cover queue replacement, shift-append, an
unreachable destination, an on-path blocker, order completion on arrival, on
target death and on cast, and a cast that has to walk into range first.

### C2 — Targeting, aggro and rules of engagement (phase 2/6)
Stances and leash, threat accumulation and decay, extended
`TargetPriorityMode`, focus fire, return-to-post, auto-engage gating by stance.
*Acceptance:* a tank holds threat against a higher-damage ally; a defensive unit
never leaves its hold radius; a `HoldPosition` unit does not path.

**Measured 2026-10-06 (headless run of the real Moba scenario, `MobaScenarioFlowDiagnostic`).**
In 300 s of game time with 362 units spawned the battle produced **27 basic attacks** (all ranged) against
**591 ability casts**; **182 of 182 melee units never landed a single basic attack** and 94.8% of all units
never attacked at all. Combat is decided by `VenomStrike` and its poison: in the 200 s instrumented run 4.3k of the 5.5k damage
was the damage-over-time tick, which reports no attacking source. Two measured causes:

- **Targets are sticky and only the assigned target can be attacked.** Target acquisition keeps the current
  target while it is alive and visible, and `IsAttackTriggerOverlapping` tests only that actor, so a unit can
  stand next to an enemy it never hits.
- **The friendly rank stands in the way.** Of 1366 "melee wants to move but is outside its attack range"
  samples, **1028 (75%) had an ally closer to the same target within two units** — the ally had stopped at
  its own attack distance (ranged 4.22) and the melee unit (attack distance 0.97) never got past it. Its
  median minimum distance to its target was 4.86 and it never came closer than 1.88 to any enemy. A duel
  measurement with nothing else on the map shows the avoidance solver alone holds two head-on actors about
  1.5 units apart, already above the melee attack distance.

**Fixed 2026-10-06.** Three changes, each measured on the same 300 s run:

- A chase now stops `OrderSystem.ChaseStopMargin` (0.15) *inside* attack reach instead of exactly on it, so a
  separation push cannot leave the attacker hovering just outside its own range with neither a move nor an
  attack left to make. Ranged basic attacks went **27 → 82**.
- The attack gate is no longer limited to the assigned target: when the assigned target is out of reach and
  another enemy is in reach, the actor retargets onto it, so being pressed against an enemy now means hitting
  it instead of chasing something further away.
- An actor no longer avoids its own current target, and an ally that shoots further no longer constrains an
  ally that has to close. A duel with nothing else on the map now closes a melee pair at full speed to
  1.5 units (the solver used to hold two head-on actors apart with no attack at all).

**Then tuned as content (2026-10-06).** Melee reach (0.25, attack distance 0.97) sits inside Cleave's 1.92
and VenomStrike's 4.72, so abilities decided the fight before a melee unit could touch anything - 305 of 310
deaths in the earlier run were the poison damage over time, and widening the melee reach to 0.9 was measured
at only 4 basic attacks per 300 s. The content change instead hardened the melee rank and softened its
ability: `ActorLoadoutAuthoring` gained a per-loadout `_healthMultiplier` (the three demo scenes use 2 for
melee, 1 for ranged, so melee fields 60 health against 30) and Cleave's damage went 8 -> 4.

Measured on the same 300 s run:

| | before | after |
|---|---|---|
| basic attacks (melee / ranged) | 27 (0 / 27) | **141 (3 / 138)** |
| melee closest approach to its target | 1.88 | **0.86** |
| units that never attacked | 94.8% | 74.9% |
| units stalled ten seconds or more | 4 | **0** |
| ability casts | 661 | 864 |

Melee now reaches contact (0.86, inside its 0.97 attack distance) and lands its first basic attacks. What
still limits melee is attrition, not reach: `VenomStrike` plus its poison remains the dominant damage
source (11.2k of the 13.9k damage dealt), so any further melee tuning is a ranged-ability decision.

### C3 — Damage and mitigation (phase 2)
`DamageType`, armor, resistances, penetration, crit, block, dodge, minimum
damage floor, and a `DamageInfo` value object carried by `ActorDamaged` so the
frame can show type, mitigation, crit and block.
*Acceptance:* table-driven mitigation tests (including `True` damage and the
floor), determinism (same seed ⇒ same crit sequence), and no damage source can
produce zero or negative damage.

### C4 — Ability model upgrade (phase 2/4)
Resource costs, cast and channel with interrupts, charges, area shapes and
ground targeting, LoS requirement, telegraph data in the frame, and AI ability
scoring that replaces "first ready in loadout order".
*Acceptance:* a cast is interrupted by damage and by a move order; a channel
pulses on its cadence and stops on stun; each area shape is covered by a
deterministic hit-set test; an out-of-resource cast is rejected.

### C5 — Status effect model upgrade (phase 2/4)
Categories, stack policies, diminishing returns, immunity tags, dispel/purge,
cast interruption on damage, and per-type resistance to control.
*Acceptance:* diminishing returns reach immunity within the window and reset
after it; purge removes only purgeable categories; a stunned unit cancels a cast
and resumes the queued order afterwards.

### C6 — Formation and movement combat (phase 6)
Tick `FormationCoordinator`; shapes with spacing and facing; re-slotting while
moving; charge/gap-close; retreat paths; flanking and rear-attack bonuses driven
by facing; path-request budget at scale.
*Acceptance:* a line formation advances without interpenetration; flanking
produces the documented damage bonus; path cost stays inside the phase-7 budget
at the target unit count.

**Landed early (movement recovery).** A dense crowd jammed: `CollisionResolver.SeparateCircles`
resolved a *full* overlap every tick, so a single separation push could be tens of times larger than
the actor's own step and shoved it back the way it came — the position was decided by separation
rather than by movement intent — and nothing anywhere detected that an actor had stopped making
progress. Measured with 60 units ordered through a gap, 3600 ticks (60 s) per run:

| Free gap | Units stalled for good | Units through the gap |
|---|---|---|
| 1.0 units | 42 → **16** | 21 → 25 |
| 1.5 units | 23 → **7** | 46 → 46 |
| 2.0 units | 22 → **7** | 51 → 50 |
| 3.0 units | 10 → **2** | 57 → 57 |

Three changes, all deterministic:

1. **Bounded separation.** `CollisionResolver.SeparateCircles` takes a relaxation factor and
   `ResolveOverlap` uses 35% of each overlap, capped at 0.12 units of correction per tick. The default
   of `relaxation = 1` keeps the exact-separation behaviour for single-pair callers.
2. **Stuck recovery.** An actor that wants to move but makes no headway for 45 ticks steps sideways
   for 20 ticks, with the side chosen from the actor index, so a jam can dissolve into a queue
   instead of everyone pressing into the same blocked spot.
3. **No hard stop.** A cohort that yields no direction no longer leaves the actor standing still; it
   steers straight at its destination. An actor that stopped there had no way to be re-routed.

Still open in C6: throughput through a choke is bounded by geometry (a crowd cannot pass a one-unit
gap faster than the gap allows), formation shapes and slot assignment are still not ticked, and there
is no queue discipline or crowd pressure beyond local avoidance.

**Fixed 2026-10-06 (same day as the measurement below).** A stuck actor now sweeps a fixed sequence of
escape headings instead of one blind sidestep: on each no-progress window the next heading is tried, the
first whose step the navigation grid accepts is held, and local avoidance is bypassed while escaping
because the solver is what returned no velocity in the first place. Standing behind that, an ally that
shoots further (`AttackRangeComponent.Reach`) no longer constrains an ally that has to close, and neither
does an actor's own current target, so a melee rank can press through a ranged rank that stopped at its
own attack range. Measured on the 300 s run: units that never acquire a target **42 → 16** of 362,
units stalled for ten seconds or more **81 → 4**, longest stall **132 s → 76 s**, while speed ratio and
path efficiency stayed at 0.89 and 0.84. `CrowdMovementTests` and `MobaScenarioTests` guard it.

**Measured 2026-10-06 (real Moba scenario, headless).** Movement itself is healthy: median speed 0.90 of
the 0.8 u/s budget, net displacement over travelled distance 0.89, and the heading sits within 8.4° of the
direction to the assigned target (only 4% of samples move away from it). Two behaviour-level problems
remain:

- **The shortest reach never arrives.** Every rank stops at its own attack distance, so the melee rank
  queues behind the ranged rank and can never get through — see the measurement under C2.
- **16% of units never acquire a target and are then pinned on the terrain.** 39 of 242 units in a 100 s
  run (42 of 362 in 300 s) never picked a target, travelled about eight units and stopped against the
  obstacle field — 8 of the 10 sampled were within three units of an obstacle centre — for the rest of the
  match. The same scenario with the obstacles removed pins **zero** units and stalls **none** (0/242
  versus 42/242 stalled for ten seconds or more, and speed ratio 0.99 versus 0.84): the pinning comes from
  the route/avoidance interaction at the obstacle field, not from the crowd. `MovementCohortCoordinator`
  routes toward the battle objective; when a route dead-ends, the sidestep recovery (`_recoveryTicks`) is
  not enough to break out.

### C7 — Vision and information (phase 4/6)
Per-faction visible/explored sets, hidden and stealth units, reveals, target
gating on visibility, and the presentation contract for unseen actors
(absence from the frame, not a hidden flag).
*Acceptance:* an enemy outside vision is absent from `ActorSnapshot` and
unattackable; a reveal effect makes it appear and targetable in the same tick;
vision cost is inside the phase-7 budget.

### C8 — Morale, objectives and battle flow (phase 4)
Morale and rout, rally points, objectives and progress in the frame, victory and
defeat evaluation, reinforcements, withdrawal, hero downed/revive, and the XP
and loot hooks that feed the RPG progression.
*Acceptance:* each objective type passes a deterministic test; a routing unit
becomes uncontrollable and recovers or leaves; a battle ends with the correct
`BattleResult` and reward payload.

## 6. Order of execution and dependencies

```text
C1 orders ──► C2 aggro/stances ──► C6 formation/movement
   │                 │
   ├──► C3 damage ───┼──► C4 abilities ──► C5 status
   │                 │
   └──► C7 vision ───┴──► C8 objectives/morale ──► phase 7 scale
```

C1 and C3 are the first two slices and both belong to roadmap phase 2; C7 must
land before any AI that needs information asymmetry; C8 needs C2 and C3 because
objectives and morale read damage, threat and stance.

## 7. Cross-cutting rules

- All combat randomness comes from one seeded simulation RNG; the seed and its
  draw count are part of the frame record so a replay reproduces a battle.
- `RPG.Core` stays engine-free: no Unity types, no `Transform`, no time source
  other than the tick and its delta.
- One declared type per file; interface members implemented explicitly.
- Presentation never decides combat: no view, tween or animation event may
  change health, threat, cooldown, resource or visibility.
- Content is validated, not repaired: a missing or illegal asset is an authoring
  error surfaced in the Inspector and in tests.
- Every combat milestone ships a deterministic EditMode scenario, a headless
  core run where possible, and one Play-mode scenario that exercises the
  presentation path.

## 8. Risks

| Risk | Mitigation |
|---|---|
| Combat scope crowds out RPG systems (progression, campaign) | Milestones are gated by the roadmap; C1+C3 land inside phase 2, the rest attach to phases 4/6 |
| Order queue + nav + avoidance interact badly (units oscillating, order thrash) | Deterministic scenario tests per interaction before adding the next layer; cohort/route revision already exists to invalidate stale routes |
| Vision cost grows with unit count | Fixed-cadence, count-based visibility grid rather than per-frame raycasts; measured against the phase-7 budget |
| Damage/status numbers become unbalanceable | All tuning lives in content assets, with a validator and (phase 8) a balance table view |
| Threat/aggro surprises in a deterministic sim | Threat is explicit, tick-quantised and covered by table tests rather than emergent from floating-point accumulation |
| Ability model growth bloats `AbilityAsset` | Effects stay a small discriminated list; new effect kinds are additive and validated, never optional fields that silently do nothing |

## 9. Non-goals

Rigid-body or destructible physics; joints, ragdoll or vehicles; naval, air or
siege layers; per-unit voice/barks; multiplayer combat (roadmap phase 9);
procedural terrain; and any combat rule that requires a Unity object to evaluate.
