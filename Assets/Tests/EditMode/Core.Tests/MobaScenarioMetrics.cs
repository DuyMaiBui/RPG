using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Core.Navigation;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    /// <summary>Measures one headless Moba scenario run: attack output, movement quality and stalls, all tracked
    /// per unit from the simulation events and component state. The numbers this produces are what the scenario
    /// tests assert and what the flow report prints.</summary>
    internal sealed class MobaScenarioMetrics
    {
        private const float StallEpsilonSquared = 0.0001f;
        private const int StallWindowTicks = 30;
        private const int LongStallTicks = 300;

        private readonly Dictionary<EntityId, MobaScenarioUnit> _units = new();
        private readonly List<EntityId> _order = new();
        private readonly List<string> _lines = new();
        private readonly int[] _attacksByFaction = new int[3];
        private readonly int[] _damageByFaction = new int[3];
        private readonly int[] _killsByFaction = new int[3];
        private readonly int[] _deathsByFaction = new int[3];
        private readonly Dictionary<int, int> _castsByAbility = new();
        private readonly List<float> _meleeAttackDistances = new();
        private readonly List<float> _rangedAttackDistances = new();
        private readonly List<float> _meleeMinTargetDistances = new();
        private readonly List<float> _meleeNearestEnemy = new();
        private readonly List<float> _meleeStopDistances = new();
        private readonly List<float> _targetAngles = new();
        private readonly List<float> _headingJitter = new();
        private const int StopGoTicks = 10;
        private int _speedDipTicks;
        private int _terrainDipTicks;
        private int _movingTicks;
        private readonly List<float> _idleTravel = new();
        private readonly List<float> _idleNetDisplacement = new();
        private int _idleReachedBase;
        private const int PinnedLifetimeTicks = 300;
        private const float PinnedTravel = 5f;
        private readonly List<MobaScenarioUnit> _idleUnits = new();
        private readonly List<string> _idleTrace = new();
        private int _totalTicks;
        private int _tick;
        private int _nextSample = int.MaxValue;
        private int _sampleEvery;
        private int _attacksThisSample;
        private int _lastAttackTick = -1;
        private int _meleeUnits;
        private int _rangedUnits;
        private int _meleeDamage;
        private int _rangedDamage;
        private int _unknownSourceDamage;
        private int _meleeDeaths;
        private int _rangedDeaths;
        private int _unknownKillerDeaths;
        private int _meleeInRangeTicks;
        private int _meleeAttacks;
        private int _rangedAttacks;
        private int _rangedInRangeTicks;
        private int _meleeContactSamples;
        private int _meleeNearestSamples;
        private int _frontBlockedAlly;
        private int _frontBlockedEnemy;
        private int _frontFree;
        private int _meleeStoppedSamples;
        private int _targetAnglesAway;
        private int _neverMoved;
        private int _neverAcquiredButMoved;
        private int _neverAttackedMelee;
        private int _neverAttackedRanged;
        private bool _ended;

        public int Spawned => _order.Count;

        public int TotalAttacks => _attacksByFaction[1] + _attacksByFaction[2];

        public int TotalCasts
        {
            get
            {
                var casts = 0;
                foreach (var pair in _castsByAbility) casts += pair.Value;
                return casts;
            }
        }

        public int MeleeUnits => _meleeUnits;

        public int RangedUnits => _rangedUnits;

        public int MeleeAttacks => _meleeAttacks;

        public int RangedAttacks => _rangedAttacks;

        public int MeleeUnitsThatAttacked => _meleeUnits - _neverAttackedMelee;

        public int MeleeUnitsInRange => _meleeInRangeTicks;

        public int MeleeContactSamples => _meleeContactSamples;

        /// <summary>Closest any melee unit got to its assigned target: the number that tells "cannot reach" apart from
        /// "reached but the ability fight decided it first".</summary>
        public float MeleeClosestApproach { get; private set; }

        /// <summary>Median turn per tick of the direction an actor actually travelled: the "walks a wobbly line"
        /// number. Straight-line movement keeps this near zero.</summary>
        public float MedianHeadingJitterDegrees { get; private set; }

        /// <summary>Share of moving ticks in which an actor covered less than half of its own step.</summary>
        public float SpeedDipShare { get; private set; }

        public int TerrainUnitsWithStopGo { get; private set; }

        public int CrowdUnitsWithStopGo { get; private set; }

        public int ContactUnitsWithStopGo { get; private set; }

        /// <summary>Stop-and-go episodes while an enemy was in reach: combat pressure, not a crowd-flow problem.</summary>
        public int ContactStopGoEpisodes { get; private set; }

        /// <summary>Episodes in which the navigation grid refused the step: static geometry.</summary>
        public int TerrainStopGoEpisodes { get; private set; }

        /// <summary>Episodes with nothing in front: the crowd itself or an unstick escape.</summary>
        public int CrowdStopGoEpisodes { get; private set; }

        public int MidlineCrossings { get; private set; }

        public int IdleUnits => _idleUnits.Count;

        /// <summary>Units that lived long enough to get somewhere and still never acquired a target and barely moved:
        /// the signature of an actor pinned against the terrain rather than a late spawn with no time left.</summary>
        public int PinnedUnits { get; private set; }

        public int FailedToCloseUnits { get; private set; }

        public int MeleeNeverAttacked => _neverAttackedMelee;

        /// <summary>Fraction of melee samples in which the front was blocked by an ally that stood closer to the
        /// same target.</summary>
        public float MeleeFrontBlockedByAllyFraction =>
            _frontBlockedAlly + _frontBlockedEnemy + _frontFree == 0
                ? 0f
                : _frontBlockedAlly / (float)(_frontBlockedAlly + _frontBlockedEnemy + _frontFree);

        public float MedianSpeedRatio { get; private set; }

        public float MedianPathEfficiency { get; private set; }

        public float MedianHeadingToTarget { get; private set; }

        public float FractionNeverAttacked => Spawned == 0 ? 0f : (_neverAttackedMelee + _neverAttackedRanged) / (float)Spawned;

        public float FractionIdle => Spawned == 0 ? 0f : _idleUnits.Count / (float)Spawned;

        public float FractionStalledLong { get; private set; }

        public string ReportText { get; private set; }

        public void Run(RpgSimulationState state, int totalTicks, int sampleEvery, float fixedDeltaTime)
        {
            _totalTicks = totalTicks;
            _sampleEvery = sampleEvery;
            _nextSample = sampleEvery;
            var context = new SimulationContext<RpgSimulationState>(state, fixedDeltaTime);
            ISimulationApplication<RpgSimulationState> application = new RpgSimulationApplication();
            var started = DateTime.UtcNow;
            for (var tick = 0; tick < totalTicks; tick++)
            {
                context.ResetForNextTick();
                var simulationTick = new SimulationTick(tick);
                application.BeginTick(context, simulationTick);
                application.Tick(context, simulationTick);
                var events = context.DrainEvents();
                application.HandleEvents(context, events);
                context.CommitDeferredActions();
                _tick = tick;
                TrackUnits(state, tick);
                CountEvents(events, state);
                if (tick + 1 >= _nextSample)
                {
                    Sample(state, tick);
                    _nextSample += _sampleEvery;
                }
            }

            Line($"# {totalTicks} ticks in {(DateTime.UtcNow - started).TotalSeconds:0.0}s");
            Report(state);
        }

        private void TrackUnits(RpgSimulationState state, int tick)
        {
            for (var index = 0; index < state.Actors.SlotCount; index++)
            {
                if (!state.Actors.TryGetAt(index, out var actor)) continue;
                if (!_units.TryGetValue(actor.Id, out var unit))
                {
                    unit = new MobaScenarioUnit
                    {
                        Id = actor.Id,
                        Faction = actor.Components.Get<FactionComponent>().Faction,
                        SpawnTick = tick,
                        SpawnPosition = actor.Components.Get<PositionComponent>().Position,
                        LastProgressTick = tick,
                    };
                    unit.LastPosition = unit.SpawnPosition;
                    unit.Reach = actor.Components.TryGet<AttackRangeComponent>(out var range) ? range.Reach : 0f;
                    unit.Ranged = actor.Components.TryGet<ProjectileWeaponComponent>(out _);
                    unit.AttackDistance = actor.Components.Get<ColliderComponent>().Compound.BoundingRadius + unit.Reach;
                    if (unit.Ranged) _rangedUnits++;
                    else _meleeUnits++;
                    _units[actor.Id] = unit;
                    _order.Add(actor.Id);
                }

                if (unit.Died) continue;
                var position = actor.Components.Get<PositionComponent>().Position;
                var delta = position - unit.LastPosition;
                unit.Travelled += (float)Math.Sqrt(delta.LengthSquared);
                unit.LastPosition = position;
                var net = position - unit.SpawnPosition;
                unit.NetDisplacement = (float)Math.Sqrt(net.LengthSquared);

                if (delta.LengthSquared > StallEpsilonSquared)
                {
                    unit.LastProgressTick = tick;
                    unit.CurrentStall = 0;
                }
                else
                {
                    unit.CurrentStall = tick - unit.LastProgressTick;
                    unit.StallTicks++;
                    if (unit.CurrentStall > unit.MaxStall) unit.MaxStall = unit.CurrentStall;
                }

                var movement = actor.Components.Get<MovementComponent>();
                unit.WantsToMove = movement.DesiredDirection.LengthSquared > 0.000001f;
                TrackCloseDistance(state, actor, position, unit);
                TrackFlowQuality(state, actor, unit, movement, position, delta);
                if (actor.Components.Get<HealthComponent>().IsDead)
                {
                    unit.Died = true;
                    unit.DeathTick = tick;
                }
            }
        }

        private bool RefusesStep(RpgSimulationState state, Actor actor, MovementComponent movement, SimulationVector2 position)
        {
            if (movement.DesiredDirection.LengthSquared <= 0.000001f) return false;
            var step = movement.EffectiveSpeed / MobaScenarioData.TickRate;
            if (step <= 0f) return false;
            var radius = actor.Components.Get<ColliderComponent>().Compound.BoundingRadius;
            var resolved = state.Navigation.ResolveMovement(position, position + movement.DesiredDirection * step, radius);
            return (resolved - position).LengthSquared <= 0.000001f;
        }

        /// <summary>Measures how smoothly an actor moved this tick: how far it turned since the last tick and whether
        /// it covered its own step. The two numbers behind "the crowd shuffles instead of flowing".</summary>
        private void TrackFlowQuality(
            RpgSimulationState state,
            Actor actor,
            MobaScenarioUnit unit,
            MovementComponent movement,
            SimulationVector2 position,
            SimulationVector2 delta)
        {
            if (unit.WantsToMove)
            {
                _movingTicks++;
                var step = movement.EffectiveSpeed / MobaScenarioData.TickRate;
                if (delta.LengthSquared < step * step * 0.25f)
                {
                    _speedDipTicks++;
                    var inContact = unit.CurrentTargetDistance >= 0f &&
                                    unit.CurrentTargetDistance <= unit.AttackDistance + 1.5f;
                    var terrain = !inContact && RefusesStep(state, actor, movement, position);
                    if (inContact)
                    {
                        unit.CurrentContactDipTicks++;
                        if (unit.CurrentContactDipTicks == StopGoTicks) unit.ContactDipEpisodes++;
                        unit.CurrentTerrainDipTicks = 0;
                        unit.CurrentCrowdDipTicks = 0;
                    }
                    else if (terrain)
                    {
                        unit.CurrentTerrainDipTicks++;
                        if (unit.CurrentTerrainDipTicks == StopGoTicks) unit.TerrainDipEpisodes++;
                        unit.CurrentContactDipTicks = 0;
                        unit.CurrentCrowdDipTicks = 0;
                    }
                    else
                    {
                        unit.CurrentCrowdDipTicks++;
                        if (unit.CurrentCrowdDipTicks == StopGoTicks) unit.CrowdDipEpisodes++;
                        unit.CurrentContactDipTicks = 0;
                        unit.CurrentTerrainDipTicks = 0;
                    }
                }
                else
                {
                    unit.CurrentContactDipTicks = 0;
                    unit.CurrentTerrainDipTicks = 0;
                    unit.CurrentCrowdDipTicks = 0;
                }
            }

            if (delta.LengthSquared <= 0.0000001f) return;
            var heading = delta.Normalized();
            if (unit.LastHeading.LengthSquared > 0.0000001f && (_tick + unit.Id.Index) % 10 == 0)
            {
                var cosine = Math.Max(-1f, Math.Min(1f, heading.X * unit.LastHeading.X + heading.Y * unit.LastHeading.Y));
                _headingJitter.Add((float)(Math.Acos(cosine) * 180.0 / Math.PI));
            }

            unit.LastHeading = heading;
        }

        private void TrackCloseDistance(RpgSimulationState state, Actor actor, SimulationVector2 position, MobaScenarioUnit unit)
        {
            unit.CurrentTargetDistance = -1f;
            if (!actor.Components.TryGet<TargetComponent>(out var targetComponent) ||
                targetComponent.CurrentTarget == EntityId.None ||
                !state.Actors.TryGet(targetComponent.CurrentTarget, out var target) ||
                target.Components.Get<HealthComponent>().IsDead)
            {
                unit.CurrentNoClose = 0;
                unit.PreviousTargetDistance = -1f;
                return;
            }

            var distance = Distance(target.Components.Get<PositionComponent>().Position, position);
            unit.CurrentTargetDistance = distance;
            if (distance < unit.MinTargetDistance) unit.MinTargetDistance = distance;
            var targetRadius = target.Components.Get<ColliderComponent>().Compound.BoundingRadius;
            if (distance <= unit.AttackDistance + targetRadius)
            {
                unit.InRangeTicks++;
                if (unit.Ranged) _rangedInRangeTicks++;
                else _meleeInRangeTicks++;
            }

            if (unit.Ranged)
            {
                SampleRangedHeading(actor, position, target, unit);
            }
            else if (unit.Travelled > 0.01f && distance > unit.AttackDistance + targetRadius)
            {
                SampleMeleeApproach(state, actor, position, unit, target, distance);
            }

            if (distance <= unit.Reach + 0.7f)
            {
                unit.CurrentNoClose = 0;
                unit.PreviousTargetDistance = distance;
                return;
            }

            if (unit.PreviousTargetDistance >= 0f && distance > unit.PreviousTargetDistance - 0.0005f)
            {
                unit.CurrentNoClose++;
                if (unit.CurrentNoClose > unit.MaxNoClose) unit.MaxNoClose = unit.CurrentNoClose;
            }
            else
            {
                unit.CurrentNoClose = 0;
            }

            unit.PreviousTargetDistance = distance;
        }

        private void SampleRangedHeading(Actor actor, SimulationVector2 position, Actor target, MobaScenarioUnit unit)
        {
            if ((_tick + unit.Id.Index) % 30 != 0) return;
            var heading = actor.Components.Get<MovementComponent>().DesiredDirection;
            if (heading.LengthSquared <= 0.000001f) return;
            var toTarget = (target.Components.Get<PositionComponent>().Position - position).Normalized();
            var cosine = Math.Max(-1f, Math.Min(1f, toTarget.X * heading.X + toTarget.Y * heading.Y));
            var degrees = (float)(Math.Acos(cosine) * 180.0 / Math.PI);
            _targetAngles.Add(degrees);
            if (degrees > 90f) _targetAnglesAway++;
        }

        /// <summary>Records how close a melee actor gets to every enemy and whether an ally closer to the same target
        /// is standing in the way: that is the difference between "cannot walk there" and "somebody is blocking".</summary>
        private void SampleMeleeApproach(
            RpgSimulationState state,
            Actor actor,
            SimulationVector2 position,
            MobaScenarioUnit unit,
            Actor target,
            float distanceToTarget)
        {
            if ((_tick + unit.Id.Index) % 30 == 0)
            {
                var faction = actor.Components.Get<FactionComponent>().Faction;
                var nearest = float.MaxValue;
                for (var index = 0; index < state.Actors.SlotCount; index++)
                {
                    if (!state.Actors.TryGetAt(index, out var other)) continue;
                    if (other.Components.Get<FactionComponent>().Faction == faction) continue;
                    if (other.Components.Get<HealthComponent>().IsDead) continue;
                    var otherDistance = Distance(other.Components.Get<PositionComponent>().Position, position);
                    if (otherDistance < nearest) nearest = otherDistance;
                }

                if (nearest < float.MaxValue)
                {
                    _meleeNearestSamples++;
                    _meleeNearestEnemy.Add(nearest);
                    if (nearest <= unit.AttackDistance + 0.72f) _meleeContactSamples++;
                }
            }

            if ((_tick + unit.Id.Index) % 60 != 0 || !unit.WantsToMove) return;
            _meleeStoppedSamples++;
            _meleeStopDistances.Add(distanceToTarget);
            var attackerFaction = actor.Components.Get<FactionComponent>().Faction;
            var targetPosition = target.Components.Get<PositionComponent>().Position;
            for (var index = 0; index < state.Actors.SlotCount; index++)
            {
                if (!state.Actors.TryGetAt(index, out var other)) continue;
                if (other.Id == actor.Id) continue;
                if (other.Components.Get<HealthComponent>().IsDead) continue;
                var otherPosition = other.Components.Get<PositionComponent>().Position;
                if (Distance(otherPosition, position) > 2f) continue;
                if (Distance(targetPosition, otherPosition) >= distanceToTarget) continue;
                if (other.Components.Get<FactionComponent>().Faction == attackerFaction) _frontBlockedAlly++;
                else _frontBlockedEnemy++;
                return;
            }

            _frontFree++;
        }

        private void CountEvents(IReadOnlyList<ISimulationEvent> events, RpgSimulationState state)
        {
            for (var index = 0; index < events.Count; index++)
            {
                switch (events[index])
                {
                    case ActorAttackStarted attack:
                        if (_units.TryGetValue(attack.Source, out var attacker))
                        {
                            attacker.Attacks++;
                            _attacksByFaction[(int)attacker.Faction]++;
                            if (attacker.Ranged) _rangedAttacks++;
                            else _meleeAttacks++;
                            _attacksThisSample++;
                            _lastAttackTick = _tick;
                            if (state.Actors.TryGet(attack.Source, out var sourceActor) &&
                                state.Actors.TryGet(attack.Target, out var attackTarget))
                            {
                                var distance = Distance(
                                    attackTarget.Components.Get<PositionComponent>().Position,
                                    sourceActor.Components.Get<PositionComponent>().Position);
                                (attacker.Ranged ? _rangedAttackDistances : _meleeAttackDistances).Add(distance);
                            }
                        }

                        break;

                    case ActorDamaged damaged:
                        if (_units.TryGetValue(damaged.Source, out var dealer))
                        {
                            dealer.DamageDealt += damaged.Damage;
                            _damageByFaction[(int)dealer.Faction] += damaged.Damage;
                            if (dealer.Ranged) _rangedDamage += damaged.Damage;
                            else _meleeDamage += damaged.Damage;
                        }
                        else
                        {
                            _unknownSourceDamage += damaged.Damage;
                        }

                        if (_units.TryGetValue(damaged.Target, out var victim)) victim.DamageTaken += damaged.Damage;
                        break;

                    case ActorDied died:
                        if (_units.TryGetValue(died.Target, out var dead))
                        {
                            dead.Died = true;
                            if (dead.DeathTick < 0) dead.DeathTick = _tick;
                            _deathsByFaction[(int)dead.Faction]++;
                        }

                        if (_units.TryGetValue(died.Source, out var killer))
                        {
                            killer.Kills++;
                            _killsByFaction[(int)killer.Faction]++;
                            if (killer.Ranged) _rangedDeaths++;
                            else _meleeDeaths++;
                        }
                        else
                        {
                            _unknownKillerDeaths++;
                        }

                        break;

                    case ActorAbilityCast cast:
                        if (_units.TryGetValue(cast.Source, out var caster)) caster.Casts++;
                        _castsByAbility.TryGetValue(cast.AbilityId, out var castCount);
                        _castsByAbility[cast.AbilityId] = castCount + 1;
                        break;
                }
            }
        }

        private void Sample(RpgSimulationState state, int tick)
        {
            var (redAlive, blueAlive, redFront, blueFront) = Alive(state);
            MidlineCrossings = 0;
            for (var index = 0; index < _order.Count; index++)
            {
                var unit = _units[_order[index]];
                if (unit.CrossedMidline) { MidlineCrossings++; continue; }
                var past = unit.Faction == FactionId.Red ? unit.LastPosition.X > 0f : unit.LastPosition.X < 0f;
                if (past && !unit.Died) unit.CrossedMidline = true;
                if (past) MidlineCrossings++;
            }
            var stalled = 0;
            for (var index = 0; index < _order.Count; index++)
            {
                var unit = _units[_order[index]];
                if (!unit.Died && unit.CurrentStall >= StallWindowTicks) stalled++;
            }

            var rate = MobaScenarioData.TickRate;
            Line($"t={tick / (float)rate,6:0.0}s red={redAlive,3} blue={blueAlive,3} "
                + $"kills R/B={_killsByFaction[1],-3}/{_killsByFaction[2],-3} "
                + $"dmg R/B={_damageByFaction[1],-6}/{_damageByFaction[2],-6} "
                + $"front R={redFront,7:0.0} B={blueFront,7:0.0} gap={redFront - blueFront,6:0.0} "
                + $"stalled={stalled} attacks={_attacksThisSample} crossed={MidlineCrossings}");
            _attacksThisSample = 0;
        }

        private static (int Red, int Blue, float RedFront, float BlueFront) Alive(RpgSimulationState state)
        {
            var red = 0;
            var blue = 0;
            var redFront = 0f;
            var blueFront = 0f;
            for (var index = 0; index < state.Actors.SlotCount; index++)
            {
                if (!state.Actors.TryGetAt(index, out var actor)) continue;
                if (actor.Components.Get<HealthComponent>().IsDead) continue;
                var position = actor.Components.Get<PositionComponent>().Position;
                if (actor.Components.Get<FactionComponent>().Faction == FactionId.Red)
                {
                    if (red == 0 || position.X > redFront) redFront = position.X;
                    red++;
                }
                else
                {
                    if (blue == 0 || position.X < blueFront) blueFront = position.X;
                    blue++;
                }
            }

            return (red, blue, redFront, blueFront);
        }

        private void Report(RpgSimulationState state)
        {
            if (_ended) return;
            _ended = true;
            var rate = MobaScenarioData.TickRate;
            var gameSeconds = _totalTicks / (float)rate;
            var totalDamage = _damageByFaction[1] + _damageByFaction[2];
            Line("");
            Line($"## Totals: spawned {Spawned}, deaths {_deathsByFaction[1] + _deathsByFaction[2]} "
                + $"(R {_deathsByFaction[1]} / B {_deathsByFaction[2]}), basic attacks {TotalAttacks} "
                + $"(R {_attacksByFaction[1]} / B {_attacksByFaction[2]}), ability casts {TotalCasts}, "
                + $"damage {totalDamage} (R {_damageByFaction[1]} / B {_damageByFaction[2]}), "
                + $"{totalDamage / 2 / gameSeconds:0.0} dmg/s per faction");

            var travelled = new List<float>();
            var efficiency = new List<float>();
            var speedRatios = new List<float>();
            var stalledLong = 0;
            var stalledVeryLong = 0;
            var stalledTotal = 0;
            var diedWithoutAttacking = 0;
            FailedToCloseUnits = 0;
            for (var index = 0; index < _order.Count; index++)
            {
                var unit = _units[_order[index]];
                var lifetimeTicks = Math.Max(1, (unit.Died ? unit.DeathTick : _totalTicks) - unit.SpawnTick);
                if (unit.Attacks == 0)
                {
                    if (unit.Ranged) _neverAttackedRanged++;
                    else _neverAttackedMelee++;
                    if (unit.Died) diedWithoutAttacking++;
                }

                if (unit.Travelled < 0.5f) _neverMoved++;
                if (unit.MaxStall >= LongStallTicks) stalledLong++;
                if (unit.MaxStall >= LongStallTicks * 3) stalledVeryLong++;
                if (unit.MaxNoClose >= LongStallTicks) FailedToCloseUnits++;
                stalledTotal += unit.StallTicks;
                travelled.Add(unit.Travelled);
                if (unit.Travelled > 0.01f) efficiency.Add(unit.NetDisplacement / unit.Travelled);
                speedRatios.Add(unit.Travelled / (lifetimeTicks / (float)rate) / MobaScenarioData.MoveSpeed);
                if (unit.MinTargetDistance == float.MaxValue)
                {
                    if (lifetimeTicks >= PinnedLifetimeTicks && unit.Travelled < PinnedTravel) PinnedUnits++;
                    _idleUnits.Add(unit);
                    _idleTravel.Add(unit.Travelled);
                    _idleNetDisplacement.Add(unit.NetDisplacement);
                    var enemyBaseX = unit.Faction == FactionId.Red
                        ? MobaScenarioData.BaseOffset
                        : -MobaScenarioData.BaseOffset;
                    if (Math.Abs(unit.LastPosition.X - enemyBaseX) <= 5f) _idleReachedBase++;
                    if (unit.NetDisplacement > 10f) _neverAcquiredButMoved++;
                }

                if (!unit.Ranged && unit.MinTargetDistance < float.MaxValue)
                    _meleeMinTargetDistances.Add(unit.MinTargetDistance);
            }

            foreach (var distance in _meleeMinTargetDistances)
                if (MeleeClosestApproach <= 0f || distance < MeleeClosestApproach) MeleeClosestApproach = distance;
            MedianSpeedRatio = Median(speedRatios);
            MedianPathEfficiency = Median(efficiency);
            MedianHeadingToTarget = Median(_targetAngles);
            MedianHeadingJitterDegrees = Median(_headingJitter);
            ContactUnitsWithStopGo = 0;
            TerrainUnitsWithStopGo = 0;
            CrowdUnitsWithStopGo = 0;
            ContactStopGoEpisodes = 0;
            TerrainStopGoEpisodes = 0;
            CrowdStopGoEpisodes = 0;
            for (var index = 0; index < _order.Count; index++)
            {
                var unit = _units[_order[index]];
                if (unit.ContactDipEpisodes > 0) ContactUnitsWithStopGo++;
                if (unit.TerrainDipEpisodes > 0) TerrainUnitsWithStopGo++;
                if (unit.CrowdDipEpisodes > 0) CrowdUnitsWithStopGo++;
                ContactStopGoEpisodes += unit.ContactDipEpisodes;
                TerrainStopGoEpisodes += unit.TerrainDipEpisodes;
                CrowdStopGoEpisodes += unit.CrowdDipEpisodes;
            }
            SpeedDipShare = _movingTicks == 0 ? 0f : _speedDipTicks / (float)_movingTicks;
            FractionStalledLong = Spawned == 0 ? 0f : stalledLong / (float)Spawned;
            TrackIdle(state);

            Line($"## Attack: never attacked {_neverAttackedMelee + _neverAttackedRanged}/{Spawned} "
                + $"({Pct(_neverAttackedMelee + _neverAttackedRanged, Spawned)}%), of which melee "
                + $"{_neverAttackedMelee}/{_meleeUnits} and ranged {_neverAttackedRanged}/{_rangedUnits}; "
                + $"died without attacking {diedWithoutAttacking}; kills R {_killsByFaction[1]} B {_killsByFaction[2]}");
            Line($"## Attack: damage melee {_meleeDamage}, ranged {_rangedDamage}, unattributed (damage over time) "
                + $"{_unknownSourceDamage}; deaths by melee {_meleeDeaths}, ranged {_rangedDeaths}, unattributed "
                + $"{_unknownKillerDeaths}; last basic attack at tick {_lastAttackTick}");
            Line($"## Attack: basic attacks melee {_meleeAttacks} ({MeleeUnitsThatAttacked}/{_meleeUnits} melee units), "
                + $"ranged {_rangedAttacks} ({_rangedUnits - _neverAttackedRanged}/{_rangedUnits} ranged units)");
            Line($"## Attack: basic attack distance melee {Describe(_meleeAttackDistances)} | ranged {Describe(_rangedAttackDistances)}");
            Line($"## Attack: melee in-range samples {_meleeInRangeTicks}, ranged {_rangedInRangeTicks}");
            var casts = new List<string>();
            foreach (var pair in _castsByAbility) casts.Add($"id{pair.Key}={pair.Value}");
            casts.Sort(StringComparer.Ordinal);
            Line($"## Attack: ability casts {string.Join(", ", casts)}");
            Line($"## Reach: melee attack reach {MobaScenarioData.MeleeAttackRange} (attack distance "
                + $"{(_meleeUnits > 0 ? 0.36f + 0.36f + MobaScenarioData.MeleeAttackRange : 0f):0.00}), melee min distance to its target "
                + $"{Describe(_meleeMinTargetDistances)}");
            Line($"## Reach: melee nearest-enemy distance {Describe(_meleeNearestEnemy)}, contact samples "
                + $"{_meleeContactSamples}/{_meleeNearestSamples}");
            Line($"## Reach: melee front samples {_meleeStoppedSamples}, blocked by an ally {_frontBlockedAlly} "
                + $"({Pct(_frontBlockedAlly, _meleeStoppedSamples)}%), by an enemy {_frontBlockedEnemy}, "
                + $"nothing in front {_frontFree}; distance to target {Describe(_meleeStopDistances)}");
            Line($"## Move: travelled {Describe(travelled)}, speed ratio {Describe(speedRatios)}, "
                + $"never moved {_neverMoved}, net/travelled {Describe(efficiency)}");
            Line($"## Flow: turn per tick {Describe(_headingJitter)} degrees, speed dips {Pct(_speedDipTicks, _movingTicks)}% "
                + $"of moving ticks; stop-and-go episodes by cause: terrain {TerrainStopGoEpisodes} "
                + $"({TerrainUnitsWithStopGo} units), crowd {CrowdStopGoEpisodes} ({CrowdUnitsWithStopGo} units), "
                + $"contact {ContactStopGoEpisodes} ({ContactUnitsWithStopGo} units); "
                + $"units past the midline {MidlineCrossings}/{Spawned}");
            Line($"## Move: heading vs direction to the target {Describe(_targetAngles)}, away in "
                + $"{Pct(_targetAnglesAway, _targetAngles.Count)}% of samples");
            Line($"## Stall: stalled >= 10s {stalledLong}/{Spawned} ({Pct(stalledLong, Spawned)}%), >= 30s "
                + $"{stalledVeryLong}, failed to close on a target >= 10s {FailedToCloseUnits}/{Spawned}, "
                + $"stall ticks {Pct(stalledTotal, Spawned * _totalTicks)}%, longest stall "
                + $"{MaxStall() / (float)rate:0.0}s");
            Line($"## Idle: pinned units (lived >= 10s, never had a target, travelled < 5) {PinnedUnits}/{Spawned}");
            Line($"## Idle: units that never acquired a target {IdleUnits}/{Spawned}, travel {Describe(_idleTravel)}, "
                + $"net displacement {Describe(_idleNetDisplacement)}, reached within 5 of the enemy base "
                + $"{_idleReachedBase}/{IdleUnits}; {_neverAcquiredButMoved} of them covered more than 10 units net");
            for (var index = 0; index < _idleTrace.Count; index++) Line(_idleTrace[index]);
            ReportText = string.Join("\n", _lines);
        }

        /// <summary>Idle units are the ones that never picked a target; the report records where they ended up so a
        /// pinned position can be told apart from a slow one.</summary>
        private void TrackIdle(RpgSimulationState state)
        {
            var obstacles = MobaScenarioData.CreateObstacles();
            var nearObstacle = 0;
            var sampled = Math.Min(10, _idleUnits.Count);
            for (var index = 0; index < sampled; index++)
            {
                var unit = _idleUnits[index];
                var nearestObstacle = float.MaxValue;
                var nearestId = -1;
                for (var obstacleIndex = 0; obstacleIndex < obstacles.Length; obstacleIndex++)
                {
                    var distance = Distance(unit.LastPosition, obstacles[obstacleIndex].Center);
                    if (distance < nearestObstacle)
                    {
                        nearestObstacle = distance;
                        nearestId = obstacleIndex;
                    }
                }

                if (nearestObstacle <= 3f) nearObstacle++;
                _idleTrace.Add($"   idle id {unit.Id.Index,4} at ({unit.LastPosition.X,7:0.0},{unit.LastPosition.Y,6:0.0}) "
                    + $"nearest obstacle {nearestId,3} at {nearestObstacle,6:0.00} travelled {unit.Travelled,6:0.0} "
                    + $"spawn {unit.SpawnTick,5} died {unit.DeathTick,5}");
            }

            if (sampled > 0)
                _idleTrace.Add($"## Idle: pinned near an obstacle (<= 3 units) {nearObstacle}/{sampled} of the sampled idle units");
        }

        private float MaxStall()
        {
            var max = 0;
            for (var index = 0; index < _order.Count; index++) max = Math.Max(max, _units[_order[index]].MaxStall);
            return max;
        }

        private static float Distance(SimulationVector2 left, SimulationVector2 right)
        {
            var delta = left - right;
            return (float)Math.Sqrt(delta.LengthSquared);
        }

        private static float Pct(int part, int total) => total == 0 ? 0f : part * 100f / total;

        private static float Median(List<float> values)
        {
            if (values.Count == 0) return 0f;
            values.Sort();
            return values[values.Count / 2];
        }

        private static string Describe(List<float> values)
        {
            if (values.Count == 0) return "n/a";
            values.Sort();
            var sum = 0f;
            for (var index = 0; index < values.Count; index++) sum += values[index];
            return string.Format(CultureInfo.InvariantCulture,
                "min {0:0.00} median {1:0.00} avg {2:0.00} max {3:0.00} (n {4})",
                values[0], values[values.Count / 2], sum / values.Count, values[values.Count - 1], values.Count);
        }

        private void Line(string text)
        {
            _lines.Add(text);
            TestContext.Progress.WriteLine("[flow] " + text);
        }
    }
}
