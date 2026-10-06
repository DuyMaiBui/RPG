using System;
using System.Runtime.InteropServices;

namespace AuraEngine.Physics.Native
{
    internal static partial class NativeMethods
    {
        private const string Library = "aura";

        /* Must equal AURA_ENGINE_ABI_VERSION in aura_types.h; bumped together with every ABI change. */
        public const uint ExpectedAbiVersion = 13u;

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint Aura_AbiVersion();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_CheckAbi(uint callerAbiVersion);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_CreateWorld(ref NativeWorldDesc desc, out NativeWorldHandle outWorld);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_DestroyWorld(NativeWorldHandle world);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_WorldBodyCount(NativeWorldHandle world, out uint outCount);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_AttachBody(NativeWorldHandle world, NativeEntityHandle entity, ref NativeBodyDesc desc, out NativeBodyHandle outBody);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_DestroyBody(NativeWorldHandle world, NativeBodyHandle body);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_SetKinematicTarget(NativeWorldHandle world, NativeBodyHandle body, ref NativePose pose);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_Step(NativeWorldHandle world, uint tick, float deltaTime);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_GetBodyState(NativeWorldHandle world, NativeBodyHandle body, out NativeBodyState outState);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_CreateWater(NativeWorldHandle world, ref NativeWaterDesc desc, out NativeWaterHandle outWater);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_DestroyWater(NativeWorldHandle world, NativeWaterHandle water);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetWaterParameters(NativeWorldHandle world, NativeWaterHandle water, ref NativeWaterDesc desc);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_ApplyWaterStep(NativeWorldHandle world, NativeWaterHandle water, float deltaTime);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_CopyBodyStates(NativeWorldHandle world, IntPtr buffer, uint capacity, out uint outCount);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_PendingEventCount(NativeWorldHandle world, out uint outCount);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_CopyEvents(NativeWorldHandle world, IntPtr buffer, uint capacity, out uint outCount);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_Raycast(NativeWorldHandle world, ref NativeRay ray, float maxDistance, ref NativeQueryFilter filter, out NativeQueryHit outHit, out byte outHasHit);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_RaycastAll(NativeWorldHandle world, ref NativeRay ray, float maxDistance, ref NativeQueryFilter filter, IntPtr buffer, uint capacity, out uint outCount);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_OverlapSphere(NativeWorldHandle world, NativeVector3 center, float radius, ref NativeQueryFilter filter, IntPtr buffer, uint capacity, out uint outCount);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_OverlapPoint(NativeWorldHandle world, NativeVector3 point, ref NativeQueryFilter filter, IntPtr buffer, uint capacity, out uint outCount);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_OverlapBox(NativeWorldHandle world, NativeVector3 center, NativeVector3 halfExtents, NativeQuaternion rotation, ref NativeQueryFilter filter, IntPtr buffer, uint capacity, out uint outCount);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_OverlapCapsule(NativeWorldHandle world, NativeVector3 pointA, NativeVector3 pointB, float radius, ref NativeQueryFilter filter, IntPtr buffer, uint capacity, out uint outCount);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_SphereCast(NativeWorldHandle world, NativeVector3 origin, float radius, NativeVector3 direction, float maxDistance, ref NativeQueryFilter filter, out NativeQueryHit outHit, out byte outHasHit);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_CapsuleCast(NativeWorldHandle world, NativeVector3 pointA, NativeVector3 pointB, float radius, NativeVector3 direction, float maxDistance, ref NativeQueryFilter filter, out NativeQueryHit outHit, out byte outHasHit);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_BoxCast(NativeWorldHandle world, NativeVector3 center, NativeVector3 halfExtents, NativeQuaternion rotation, NativeVector3 direction, float maxDistance, ref NativeQueryFilter filter, out NativeQueryHit outHit, out byte outHasHit);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_CopyContacts(NativeWorldHandle world, IntPtr buffer, uint capacity, out uint outCount);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_SetSurfaceVelocity(NativeWorldHandle world, NativeBodyHandle body, NativeVector3 velocity);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_CreateCharacter(NativeWorldHandle world, ref NativeCharacterDesc desc, out ulong outCharacter);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_DestroyCharacter(NativeWorldHandle world, ulong character);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_GetCharacterState(NativeWorldHandle world, ulong character, out NativeCharacterState outState);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_MoveCharacter(NativeWorldHandle world, ulong character, NativeVector3 desiredTranslation, float deltaTime);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_CreateVehicle(NativeWorldHandle world, ref NativeVehicleDesc desc, out NativeVehicleHandle outVehicle);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_DestroyVehicle(NativeWorldHandle world, NativeVehicleHandle vehicle);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_SetVehicleInput(NativeWorldHandle world, NativeVehicleHandle vehicle, float forward, float steering, float brake, float handBrake);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_GetVehicleWheelState(NativeWorldHandle world, NativeVehicleHandle vehicle, uint wheelIndex, out NativeVehicleWheelState outState);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_CreateSoftBody(NativeWorldHandle world, ref NativeSoftBodyDesc desc, out NativeSoftBodyHandle outSoftBody);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_DestroySoftBody(NativeWorldHandle world, NativeSoftBodyHandle softBody);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_GetSoftBodyState(NativeWorldHandle world, NativeSoftBodyHandle softBody, IntPtr vertexPositions, uint vertexCapacity, out NativeSoftBodyState outState);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_SerializeState(NativeWorldHandle world, IntPtr buffer, uint capacity, out uint outSize);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_DeserializeState(NativeWorldHandle world, IntPtr buffer, uint size);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_ComputeStateHash(NativeWorldHandle world, out ulong outHash);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_CreateJoint(NativeWorldHandle world, ref NativeJointDesc desc, out ulong outJoint);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_DestroyJoint(NativeWorldHandle world, ulong joint);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_HasJoint(NativeWorldHandle world, ulong joint, out byte outHas);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_CreateRagdoll(NativeWorldHandle world, ref NativeRagdollDesc desc, out NativeRagdollHandle outRagdoll);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_DestroyRagdoll(NativeWorldHandle world, NativeRagdollHandle ragdoll);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_GetRagdollPose(NativeWorldHandle world, NativeRagdollHandle ragdoll, IntPtr buffer, uint capacity, out uint outCount);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Aura_SetRagdollPose(NativeWorldHandle world, NativeRagdollHandle ragdoll, IntPtr poses, uint poseCount);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetLinearVelocity(NativeWorldHandle world, NativeBodyHandle body, NativeVector3 velocity);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetAngularVelocity(NativeWorldHandle world, NativeBodyHandle body, NativeVector3 velocity);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_AddForce(NativeWorldHandle world, NativeBodyHandle body, NativeVector3 force);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_AddImpulse(NativeWorldHandle world, NativeBodyHandle body, NativeVector3 impulse);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_AddTorque(NativeWorldHandle world, NativeBodyHandle body, NativeVector3 torque);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_AddAngularImpulse(NativeWorldHandle world, NativeBodyHandle body, NativeVector3 impulse);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetBodyPose(NativeWorldHandle world, NativeBodyHandle body, ref NativePose pose, byte zeroVelocity);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetGravityScale(NativeWorldHandle world, NativeBodyHandle body, float gravityScale);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetFriction(NativeWorldHandle world, NativeBodyHandle body, float friction);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetRestitution(NativeWorldHandle world, NativeBodyHandle body, float restitution);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetMotionType(NativeWorldHandle world, NativeBodyHandle body, int bodyType);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetBodyLayer(NativeWorldHandle world, NativeBodyHandle body, uint layer, ulong collisionMask);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetBodyEnabled(NativeWorldHandle world, NativeBodyHandle body, byte enabled);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_IsBodyEnabled(NativeWorldHandle world, NativeBodyHandle body, out byte outEnabled);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetJointMotor(NativeWorldHandle world, ulong joint, ref NativeJointMotorDesc motor);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetJointLimits(NativeWorldHandle world, ulong joint, byte enabled, float minLimit, float maxLimit);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_SetJointBreakThreshold(NativeWorldHandle world, ulong joint, float maxForce, float maxTorque);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_IsJointBroken(NativeWorldHandle world, ulong joint, out byte outBroken);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] public static extern int Aura_GetJointFeedback(NativeWorldHandle world, ulong joint, out NativeJointFeedback outFeedback);
    }
}
