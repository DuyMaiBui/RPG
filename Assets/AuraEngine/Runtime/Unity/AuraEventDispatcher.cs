using AuraEngine.Core;
using AuraEngine.Simulation;

namespace AuraEngine.Unity
{
    public sealed class AuraEventDispatcher
    {
        private AuraPhysicsEvent[] _buffer = new AuraPhysicsEvent[64];

        public void Dispatch(AuraSimulationWorld world, AuraViewRegistry registry)
        {
            var required = world.PendingEventCount;
            if (required == 0)
                return;

            if (_buffer.Length < required)
                _buffer = new AuraPhysicsEvent[required];

            var count = world.CopyEvents(_buffer);
            for (var index = 0; index < count; index++)
            {
                var value = _buffer[index];
                if (registry.TryGet(value.EntityA, out var viewA))
                    viewA.Dispatch(value);

                if (!(value.EntityB == value.EntityA) && registry.TryGet(value.EntityB, out var viewB))
                    viewB.Dispatch(value);
            }
        }
    }
}
