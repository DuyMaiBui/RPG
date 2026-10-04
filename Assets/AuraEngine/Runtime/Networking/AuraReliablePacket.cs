namespace AuraEngine.Networking
{
    public readonly struct AuraReliablePacket
    {
        public AuraReliablePacket(uint sequence, uint acknowledgement, ushort fragmentIndex, ushort fragmentCount, byte[] payload)
        {
            Sequence = sequence;
            Acknowledgement = acknowledgement;
            FragmentIndex = fragmentIndex;
            FragmentCount = fragmentCount;
            Payload = payload ?? System.Array.Empty<byte>();
        }

        public uint Sequence { get; }
        public uint Acknowledgement { get; }
        public ushort FragmentIndex { get; }
        public ushort FragmentCount { get; }
        public byte[] Payload { get; }
    }
}
