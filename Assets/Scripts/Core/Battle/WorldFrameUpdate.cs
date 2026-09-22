using System;
using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class WorldFrameUpdate : ISimulationUpdate
    {
        public const ushort MessageTypeId = 1;

        public WorldFrameUpdate(ActorSnapshot[] actors, ProjectileSnapshot[] projectiles, PresentationSignal[] signals, int round, int turnNumber, EntityId activeActorId, BattleResult result)
        {
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            if (projectiles == null) throw new ArgumentNullException(nameof(projectiles));
            if (signals == null) throw new ArgumentNullException(nameof(signals));
            Actors = ((ActorSnapshot[])actors.Clone()).AsMemory();
            Projectiles = ((ProjectileSnapshot[])projectiles.Clone()).AsMemory();
            Signals = ((PresentationSignal[])signals.Clone()).AsMemory();
            Round = round;
            TurnNumber = turnNumber;
            ActiveActorId = activeActorId;
            Result = result;
        }

        ushort ISimulationUpdate.TypeId => MessageTypeId;
        public ReadOnlyMemory<ActorSnapshot> Actors { get; }
        public ReadOnlyMemory<ProjectileSnapshot> Projectiles { get; }
        public ReadOnlyMemory<PresentationSignal> Signals { get; }
        public int Round { get; }
        public int TurnNumber { get; }
        public EntityId ActiveActorId { get; }
        public BattleResult Result { get; }
    }
}
