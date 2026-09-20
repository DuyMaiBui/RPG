namespace RPG.Simulation.Contracts
{
    public readonly struct ProtocolVersion
    {
        public static readonly ProtocolVersion Current = new ProtocolVersion(1, 0);

        public ProtocolVersion(ushort major, ushort minor)
        {
            Major = major;
            Minor = minor;
        }

        public ushort Major { get; }
        public ushort Minor { get; }
        public bool IsCompatibleWith(ProtocolVersion server) => Major == server.Major && Minor <= server.Minor;
    }
}
