namespace AuraEngine.Networking
{
    public readonly struct AuraSnapshotBaseline
    {
        public AuraSnapshotBaseline(uint snapshotId, uint serverTick)
        {
            SnapshotId = snapshotId;
            ServerTick = serverTick;
        }

        public uint SnapshotId { get; }
        public uint ServerTick { get; }
    }
}
