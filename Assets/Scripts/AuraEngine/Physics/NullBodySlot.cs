using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal struct NullBodySlot
    {
        public int Generation;
        public bool Occupied;
        public AuraPose Pose;
        public bool IsAwake;
    }
}
