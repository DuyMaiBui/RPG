using System.Collections.Generic;
using RPG.Core.Actors;
using RPG.Simulation.Contracts;
using SimulationEntityId = RPG.Simulation.Contracts.EntityId;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class TurnBattleDemoDriver : MonoBehaviour
    {
        private IReadOnlyDictionary<SimulationEntityId, ISimulationClient> _clients;
        private long _lastCommandTurn = -1;
        private readonly Dictionary<SimulationEntityId, long> _sequences = new();

        public void Initialize(IReadOnlyDictionary<SimulationEntityId, ISimulationClient> clients)
        {
            _clients = clients;
        }

        public void OnFrame(WorldFrameUpdate frame)
        {
            if (_clients == null || frame.Result != BattleResult.Ongoing || frame.ActiveActorId.IsNone || frame.TurnNumber == _lastCommandTurn)
                return;
            if (!_clients.TryGetValue(frame.ActiveActorId, out var client)) return;

            ActorSnapshot? active = null;
            foreach (var snapshot in frame.Actors.Span)
            {
                if (snapshot.Entity == frame.ActiveActorId)
                {
                    active = snapshot;
                    break;
                }
            }

            if (!active.HasValue) return;
            var target = FindTarget(frame, active.Value.Faction);
            if (target.IsNone) return;

            if (!_sequences.TryGetValue(frame.ActiveActorId, out var sequence))
                sequence = 0;
            var command = new ClientCommandEnvelope(
                ProtocolVersion.Current,
                new ClientSequence(sequence++),
                new SimulationTick(frame.TurnNumber),
                new AttackCommand(target));
            _sequences[frame.ActiveActorId] = sequence;
            if (client.TrySend(command))
                _lastCommandTurn = frame.TurnNumber;
        }

        private static SimulationEntityId FindTarget(WorldFrameUpdate frame, FactionId attackerFaction)
        {
            foreach (var snapshot in frame.Actors.Span)
            {
                if (snapshot.Faction != attackerFaction && snapshot.CurrentHealth > 0)
                    return snapshot.Entity;
            }

            return SimulationEntityId.None;
        }
    }
}
