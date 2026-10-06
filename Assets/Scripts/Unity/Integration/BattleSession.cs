using System;
using System.Collections.Generic;
using RPG.Content;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;
using SimulationEntityId = RPG.Simulation.Contracts.EntityId;
using VContainer.Unity;

namespace RPG.Unity
{
    /// <summary>Owns one battle session: it builds the simulation state, spawns the actors, runs the fixed-tick host
    /// and tears everything down in reverse order. <see cref="BattleSessionLifetimeScope"/> registers it as an entry
    /// point, so <see cref="VContainer.Unity.IStartable.Start"/> runs from the player loop and the container disposes
    /// it when the session scope ends.</summary>
    public sealed class BattleSession : IStartable, IDisposable
    {
        private readonly BattleRulesAsset _rules;
        private readonly ContentCatalog _catalog;
        private readonly BattleSessionSetup _setup;
        private readonly ActorViewRegistry _views;
        private readonly SimulationUnityBridge _bridge;
        private readonly RpgSimulationState _state;
        private readonly SimulationHost<RpgSimulationState> _host;
        private readonly List<ISimulationClient> _clients = new();
        private readonly List<SimulationVector2> _spawnedPositions = new();
        private bool _started;

        public BattleSession(
            BattleRulesAsset rules,
            ContentCatalog catalog,
            BattleSessionSetup setup,
            ActorViewRegistry views,
            SimulationUnityBridge bridge)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _views = views ?? throw new ArgumentNullException(nameof(views));
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

            _state = new RpgSimulationState(rules.NavigationWidth, rules.NavigationHeight, rules.NavigationCellSize);
            for (var index = 0; index < _setup.NavigationObstacles.Length; index++)
                _state.Navigation.ApplyObstacle(_setup.NavigationObstacles[index]);

            _host = new SimulationHost<RpgSimulationState>(
                _state,
                new RpgSimulationApplication(),
                new SimulationOptions(rules.TickRate));
        }

        void IStartable.Start()
        {
            if (_started) return;
            _started = true;

            if (_setup.EnableWaves)
                ConfigureWaves();

            var actorsPerFaction = _rules.ActorsPerFaction;
            var orderedIds = new List<SimulationEntityId>(actorsPerFaction * 2);
            var clientsByActor = new Dictionary<SimulationEntityId, ISimulationClient>();
            for (var index = 0; index < actorsPerFaction; index++)
            {
                orderedIds.Add(SpawnActor(clientsByActor, FactionId.Red, index));
                orderedIds.Add(SpawnActor(clientsByActor, FactionId.Blue, index));
            }

            ISimulationClient bridgeClient;
            var bridgeActorId = SimulationEntityId.None;
            if (orderedIds.Count > 0)
            {
                bridgeActorId = orderedIds[0];
                bridgeClient = clientsByActor[bridgeActorId];
            }
            else
            {
                bridgeClient = new LocalSimulationClient<RpgSimulationState>(
                    _host,
                    new SessionContext(new SessionId(Guid.NewGuid()), new PlayerId("observer")));
                _clients.Add(bridgeClient);
            }

            _bridge.Initialize(
                bridgeClient,
                _views,
                _setup.ProjectilePrefab,
                _setup.ProjectileRoot,
                bridgeActorId,
                _setup.MoveSpeed,
                1f / _rules.TickRate);
            _host.Start();
        }

        void IDisposable.Dispose()
        {
            _bridge.Release();
            for (var index = 0; index < _clients.Count; index++)
                _clients[index].Dispose();
            _clients.Clear();
            (_host as IDisposable)?.Dispose();
        }

        private SimulationEntityId SpawnActor(
            Dictionary<SimulationEntityId, ISimulationClient> clientsByActor,
            FactionId faction,
            int index)
        {
            var kind = faction == FactionId.Red ? ActorKind.Player : ActorKind.Monster;
            var actorsPerFaction = _rules.ActorsPerFaction;
            var position = new SimulationVector2(
                faction == FactionId.Red ? -2f : 2f,
                (index - (actorsPerFaction - 1) * 0.5f) * 1.5f);
            if (actorsPerFaction > 10)
            {
                var columns = (int)MathF.Ceiling(MathF.Sqrt(actorsPerFaction));
                var row = index / columns;
                var column = index % columns;
                var rows = (int)MathF.Ceiling(actorsPerFaction / (float)columns);
                var spacing = MathF.Max(0.8f, _rules.NavigationCellSize * 0.8f);
                var side = faction == FactionId.Red ? -1f : 1f;
                position = new SimulationVector2(
                    side * _rules.NavigationWidth * _rules.NavigationCellSize * 0.32f - side * column * spacing,
                    (row - (rows - 1) * 0.5f) * spacing);
            }

            position = FindSpawnPosition(position);
            var loadout = _setup.ActorLoadouts[index % _setup.ActorLoadouts.Length];
            if (loadout == null)
            {
                throw new InvalidOperationException(
                    $"BattleSession actor loadout at index {index % _setup.ActorLoadouts.Length} is missing.");
            }

            var id = _state.Actors.Spawn(kind, faction, loadout.CreateSpawnData(
                _catalog,
                _setup.MaximumHealth,
                _setup.AttackPower,
                position,
                _setup.ActorRadius,
                _setup.MoveSpeed,
                _setup.VisionRange,
                0.8f,
                colliderShapes: _setup.ActorColliderShapes));
            _state.Players.Assign(new PlayerId($"{faction}-{index}"), id);

            var session = new SessionContext(new SessionId(Guid.NewGuid()), new PlayerId($"{faction}-{index}"));
            var client = new LocalSimulationClient<RpgSimulationState>(_host, session);
            _clients.Add(client);
            clientsByActor.Add(id, client);

            _views.Create(id, faction, position, $"{faction}_{index}");
            _spawnedPositions.Add(position);
            return id;
        }

        private void ConfigureWaves()
        {
            var loadouts = _setup.ActorLoadouts;
            if (loadouts.Length < 2 || loadouts[0] == null || loadouts[1] == null)
                throw new InvalidOperationException("Wave mode requires melee and ranged actor loadouts at indices 0 and 1.");

            var resolvedBaseOffset = _rules.ResolveBaseOffset();
            var redBase = new SimulationVector2(-resolvedBaseOffset, 0f);
            var blueBase = new SimulationVector2(resolvedBaseOffset, 0f);
            _state.ConfigureBases(redBase, blueBase, _setup.ActorRadius * 2f);

            var redSpawn = new SimulationVector2(redBase.X + _setup.ActorRadius * 3f, redBase.Y);
            var blueSpawn = new SimulationVector2(blueBase.X - _setup.ActorRadius * 3f, blueBase.Y);
            var redMelee = loadouts[0].CreateSpawnData(
                _catalog,
                _setup.MaximumHealth,
                _setup.AttackPower,
                SimulationVector2.Zero,
                _setup.ActorRadius,
                _setup.MoveSpeed,
                _setup.VisionRange,
                1f,
                colliderShapes: _setup.ActorColliderShapes);
            var redRanged = loadouts[1].CreateSpawnData(
                _catalog,
                _setup.MaximumHealth,
                _setup.AttackPower,
                SimulationVector2.Zero,
                _setup.ActorRadius,
                _setup.MoveSpeed,
                _setup.VisionRange,
                1f,
                colliderShapes: _setup.ActorColliderShapes);

            _state.Waves.Configure(
                _setup.MeleePerWave,
                _setup.RangedPerWave,
                _setup.WaveUnitInterval,
                _setup.WaveInterval,
                _setup.WaveInitialDelay,
                redMelee,
                redRanged,
                redMelee,
                redRanged,
                redSpawn,
                blueSpawn);
        }

        private SimulationVector2 FindSpawnPosition(SimulationVector2 desired)
        {
            const float spacing = 0.8f;
            const int maxSearchRings = 24;
            for (var ring = 0; ring <= maxSearchRings; ring++)
            {
                var radius = ring * spacing;
                var samples = ring == 0 ? 1 : ring * 8;
                for (var sample = 0; sample < samples; sample++)
                {
                    var angle = ring == 0 ? 0f : sample * MathF.PI * 2f / samples;
                    var candidate = new SimulationVector2(
                        desired.X + MathF.Cos(angle) * radius,
                        desired.Y + MathF.Sin(angle) * radius);
                    if (!_state.Navigation.IsPositionWalkable(candidate, _setup.ActorRadius) || IsSpawnOverlapping(candidate))
                        continue;

                    return candidate;
                }
            }

            throw new InvalidOperationException($"Unable to find a walkable spawn position near {desired.X}, {desired.Y}.");
        }

        private bool IsSpawnOverlapping(SimulationVector2 candidate)
        {
            var minimumDistance = _setup.ActorRadius * 2f;
            var minimumDistanceSquared = minimumDistance * minimumDistance;
            for (var index = 0; index < _spawnedPositions.Count; index++)
            {
                if ((candidate - _spawnedPositions[index]).LengthSquared < minimumDistanceSquared)
                    return true;
            }

            return false;
        }
    }
}
