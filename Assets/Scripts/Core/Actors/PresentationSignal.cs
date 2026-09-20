using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public readonly struct PresentationSignal
    {
        public PresentationSignal(PresentationSignalKind kind, EntityId entity, EntityId source, int value)
        {
            Kind = kind;
            Entity = entity;
            Source = source;
            Value = value;
        }

        public PresentationSignalKind Kind { get; }
        public EntityId Entity { get; }
        public EntityId Source { get; }
        public int Value { get; }
    }
}
