using System;
using System.Runtime.InteropServices;
using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    public sealed class NativePhysicsBackend : IPhysicsBackend
    {
        string IPhysicsBackend.Name => "AuraEngine.Native";

        AuraPhysicsCapabilities IPhysicsBackend.Capabilities =>
            AuraPhysicsCapabilities.BodyStatic |
            AuraPhysicsCapabilities.BodyDynamic |
            AuraPhysicsCapabilities.BodyKinematic |
            AuraPhysicsCapabilities.ShapeBox |
            AuraPhysicsCapabilities.ShapeSphere |
            AuraPhysicsCapabilities.ShapeCapsule |
            AuraPhysicsCapabilities.ShapeCylinder |
            AuraPhysicsCapabilities.ShapeConvexMesh |
            AuraPhysicsCapabilities.ShapeTriangleMesh |
            AuraPhysicsCapabilities.ShapePlane |
            AuraPhysicsCapabilities.ShapeTaperedCapsule |
            AuraPhysicsCapabilities.ShapeTaperedCylinder |
            AuraPhysicsCapabilities.QueryRaycast |
            AuraPhysicsCapabilities.QueryOverlap |
            AuraPhysicsCapabilities.Triggers |
            AuraPhysicsCapabilities.Contacts |
            AuraPhysicsCapabilities.SleepWake;

        IPhysicsWorld IPhysicsBackend.CreateWorld(in AuraWorldDefinition definition) =>
            new NativePhysicsWorld(definition);

        public static bool IsAvailable()
        {
            try
            {
                return NativeMethods.Aura_CheckAbi(NativeMethods.Aura_AbiVersion()) == (int)AuraResult.Success;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
        }
    }
}
