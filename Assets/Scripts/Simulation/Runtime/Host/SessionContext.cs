using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    public readonly struct SessionContext
    {
        public SessionContext(SessionId id, PlayerId player)
        {
            Id = id;
            Player = player;
        }

        public SessionId Id { get; }
        public PlayerId Player { get; }
    }
}
