using System;
using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class WorldFrameUpdate : ISimulationUpdate
    {
        public const ushort MessageTypeId = 1;

        public WorldFrameUpdate(ActorSnapshot[] actors, PresentationSignal[] signals)
        {
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            if (signals == null) throw new ArgumentNullException(nameof(signals));
            Actors = ((ActorSnapshot[])actors.Clone()).AsMemory();
            Signals = ((PresentationSignal[])signals.Clone()).AsMemory();
        }

        public ushort TypeId => MessageTypeId;
        public ReadOnlyMemory<ActorSnapshot> Actors { get; }
        public ReadOnlyMemory<PresentationSignal> Signals { get; }
    }
}
