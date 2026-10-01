using AuraEngine.Core;

namespace AuraEngine.Networking
{
    public readonly struct AuraInputCommand : IAuraCommand
    {
        public const ushort CommandTypeId = 4096;

        public AuraInputCommand(uint clientId, uint sequence, int moveX, int moveY)
        {
            ClientId = clientId;
            Sequence = sequence;
            MoveX = moveX;
            MoveY = moveY;
        }

        public uint ClientId { get; }
        public uint Sequence { get; }
        public int MoveX { get; }
        public int MoveY { get; }

        ushort IAuraCommand.TypeId => CommandTypeId;
    }
}
