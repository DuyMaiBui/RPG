namespace AuraEngine.Networking
{
    public readonly struct AuraDeltaEntry
    {
        public AuraDeltaEntry(
            uint bodyIndex,
            short positionX,
            short positionY,
            short positionZ,
            short velocityX,
            short velocityY,
            short velocityZ,
            ushort awake,
            ushort generation)
        {
            BodyIndex = bodyIndex;
            PositionX = positionX;
            PositionY = positionY;
            PositionZ = positionZ;
            VelocityX = velocityX;
            VelocityY = velocityY;
            VelocityZ = velocityZ;
            Awake = awake;
            Generation = generation;
        }

        public uint BodyIndex { get; }
        public short PositionX { get; }
        public short PositionY { get; }
        public short PositionZ { get; }
        public short VelocityX { get; }
        public short VelocityY { get; }
        public short VelocityZ { get; }
        public ushort Awake { get; }
        public ushort Generation { get; }
    }
}
