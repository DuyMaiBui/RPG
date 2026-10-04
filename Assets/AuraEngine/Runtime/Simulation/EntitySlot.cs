using AuraEngine.Core;

namespace AuraEngine.Simulation
{
    internal struct EntitySlot
    {
        public int Generation;
        public bool Alive;
        public bool HasBody;
        public PhysicsBodyId Body;
    }
}
