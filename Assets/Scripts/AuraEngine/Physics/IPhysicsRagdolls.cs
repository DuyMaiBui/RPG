using System;
using AuraEngine.Core;
namespace AuraEngine.Physics
{
    public interface IPhysicsRagdolls
    {
        AuraRagdollId CreateRagdoll(in AuraRagdollDefinition definition);
        AuraResult DestroyRagdoll(AuraRagdollId ragdoll);
        AuraResult GetPose(AuraRagdollId ragdoll, Span<AuraPose> poses);
        AuraResult SetPose(AuraRagdollId ragdoll, ReadOnlySpan<AuraPose> poses);
    }
}
