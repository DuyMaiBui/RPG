using System;

namespace AuraEngine.Core
{
    public readonly struct AuraPhysicsQueryFilter
    {
        public AuraPhysicsQueryFilter(
            AuraPhysicsLayerMask layerMask,
            AuraTriggerInteraction triggerInteraction = AuraTriggerInteraction.UseGlobal,
            SimulationEntityId ignoredEntity = default,
            PhysicsBodyId ignoredBody = default,
            AuraQueryFlags flags = AuraQueryFlags.ClosestHit)
        {
            LayerMask = layerMask;
            TriggerInteraction = triggerInteraction;
            IgnoredEntity = ignoredEntity;
            IgnoredBody = ignoredBody;
            Flags = flags;
        }

        public AuraPhysicsLayerMask LayerMask { get; }
        public AuraTriggerInteraction TriggerInteraction { get; }
        public SimulationEntityId IgnoredEntity { get; }
        public PhysicsBodyId IgnoredBody { get; }
        public AuraQueryFlags Flags { get; }

        public static AuraPhysicsQueryFilter All => new AuraPhysicsQueryFilter(AuraPhysicsLayerMask.All);

        public bool ShouldIgnore(SimulationEntityId entity, PhysicsBodyId body)
        {
            if ((Flags & AuraQueryFlags.IgnoreSelf) != 0)
            {
                if (!IgnoredEntity.IsNone && entity == IgnoredEntity)
                    return true;

                if (IgnoredBody.IsValid && body == IgnoredBody)
                    return true;
            }

            return false;
        }

        public bool FiltersTriggers(bool isTrigger, bool globalIgnoreTriggers)
        {
            switch (TriggerInteraction)
            {
                case AuraTriggerInteraction.Ignore:
                    return isTrigger;
                case AuraTriggerInteraction.Collide:
                    return false;
                default:
                    return isTrigger && globalIgnoreTriggers;
            }
        }
    }
}
