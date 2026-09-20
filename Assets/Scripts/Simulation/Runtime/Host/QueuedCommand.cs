using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    internal readonly struct QueuedCommand
    {
        public QueuedCommand(SessionContext session, ClientCommandEnvelope command)
        {
            Session = session;
            Command = command;
        }

        public SessionContext Session { get; }
        public ClientCommandEnvelope Command { get; }
    }
}
