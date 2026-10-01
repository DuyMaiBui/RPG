using System;
using System.Runtime.InteropServices;

namespace AuraEngine.Physics.Native
{
    internal static class NativeMethods
    {
        private const string Library = "aura";

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
    }
}
